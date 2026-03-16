using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.WebSockets;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class ProcessService : IProcessService, IDisposable
{
    private const uint CtrlCEvent = 0;
    private const uint CtrlBreakEvent = 1;
    private const int HeartbeatIntervalMs = 3000;
    private const int HeartbeatTimeoutMs = 2000;
    private const int RecoveryWindowMs = 90000;
    private const int WebSocketReconnectDelayMs = 2000;
    private const int WebSocketInitialConnectDelayMs = 6000;

    private readonly ArgumentBuilder _argumentBuilder;
    private readonly IPythonPathService _pythonPathService;
    private readonly IProxyService _proxyService;
    private readonly ILogService _logService;
    private readonly HttpClient _httpClient;
    private readonly Timer _heartbeatTimer;
    private Process? _process;
    private ProcessStatus _status = new();
    private string _comfyApiUrl = "http://127.0.0.1:8188/system_stats";
    private string _comfyWsUrl = "ws://127.0.0.1:8188/ws";
    private string? _lastPythonPath;
    private string? _lastMainPath;
    private string _lastSystemStats = "暂无 ComfyUI system_stats 数据";
    private bool _isHeartbeatEnabled;
    private bool _lastHeartbeatSuccess;
    private bool _isDisposed;
    private bool _stopRequestedByUser;
    private DateTime? _recoveryDeadlineUtc;
    private readonly object _statusLock = new();
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _webSocketCts;
    private Task? _webSocketMonitorTask;
    private bool _webSocketWaitingForServerNotified;
    private bool _webSocketDisconnectedNotified;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);

    public event EventHandler<ProcessStatus>? StatusChanged;
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<bool>? HeartbeatStatusChanged;
    public event EventHandler<string>? SystemStatsUpdated;

    public ProcessService(
        ArgumentBuilder argumentBuilder,
        IPythonPathService pythonPathService,
        IProxyService proxyService,
        ILogService logService)
    {
        _argumentBuilder = argumentBuilder;
        _pythonPathService = pythonPathService;
        _proxyService = proxyService;
        _logService = logService;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(HeartbeatTimeoutMs) };
        _heartbeatTimer = new Timer(OnHeartbeatTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void ConfigureApiEndpoint(string listen, int port)
    {
        var normalizedListen = listen == "0.0.0.0" ? "127.0.0.1" : listen;
        _comfyApiUrl = $"http://{normalizedListen}:{port}/system_stats";
        _comfyWsUrl = $"ws://{normalizedListen}:{port}/ws";
    }

    public async Task<ProcessStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var snapshot = CreateStatusSnapshot();
        return snapshot.IsRunning ? snapshot : null;
    }

    public Task<bool> StartAsync(string comfyRootPath, ComfyConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (_process is { HasExited: false })
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(comfyRootPath))
        {
            return Task.FromResult(false);
        }

        var arguments = _argumentBuilder.BuildArguments(configuration);
        var startInfo = BuildStartInfo(comfyRootPath, arguments);
        if (startInfo == null)
        {
            OutputReceived?.Invoke(this, "无法启动：未能定位 Python 或 main.py，请在仪表盘确认 ComfyUI 路径与 Python 环境。");
            return Task.FromResult(false);
        }

        ConfigureApiEndpoint(configuration.Network.Listen, configuration.Network.Port);
        _stopRequestedByUser = false;
        _recoveryDeadlineUtc = null;

        var fullCommand = string.IsNullOrWhiteSpace(startInfo.Arguments)
            ? $"\"{startInfo.FileName}\""
            : $"\"{startInfo.FileName}\" {startInfo.Arguments}";
        OutputReceived?.Invoke(this, $"启动命令：{fullCommand}");

        _status = new ProcessStatus
        {
            VersionId = "local",
            State = ProcessState.Starting,
            IsRunning = true
        };
        StatusChanged?.Invoke(this, CreateStatusSnapshot());

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (_statusLock)
                {
                    _status.OutputLogs.Add(e.Data);
                }
                OutputReceived?.Invoke(this, e.Data);
            }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (_statusLock)
                {
                    _status.OutputLogs.Add(e.Data);
                }
                OutputReceived?.Invoke(this, e.Data);
            }
        };

        _process.Exited += OnProcessExited;

        var started = _process.Start();
        if (started)
        {
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            lock (_statusLock)
            {
                _status.State = ProcessState.Running;
                _status.StartTime = DateTime.Now;
                _status.ProcessId = _process.Id;
                _status.IsRunning = true;
            }
            StatusChanged?.Invoke(this, CreateStatusSnapshot());
            StartHeartbeat();
            StartWebSocketMonitor();
        }
        else
        {
            lock (_statusLock)
            {
                _status.State = ProcessState.Error;
                _status.IsRunning = false;
            }
            StatusChanged?.Invoke(this, CreateStatusSnapshot());
        }

        return Task.FromResult(started);
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_stopRequestedByUser)
        {
            OutputReceived?.Invoke(this, "[ProcessService] 进程已按用户请求退出");
            MarkStopped("已停止");
            return;
        }

        OutputReceived?.Invoke(this, "[ProcessService] 检测到进程退出，进入恢复观察窗口...");
        lock (_statusLock)
        {
            _recoveryDeadlineUtc = DateTime.UtcNow.AddMilliseconds(RecoveryWindowMs);
            _status.State = ProcessState.Recovering;
            _status.IsRunning = true;
            _status.ProcessId = null;
        }

        StatusChanged?.Invoke(this, CreateStatusSnapshot());
    }

    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        _stopRequestedByUser = true;
        _recoveryDeadlineUtc = null;

        if (_process == null || _process.HasExited)
        {
            var killed = TryKillLingeringComfyPythonProcesses();
            MarkStopped(killed ? "检测到残留进程并已清理" : "已停止");
            return Task.FromResult(killed);
        }

        lock (_statusLock)
        {
            _status.State = ProcessState.Stopping;
            _status.IsRunning = true;
        }
        StatusChanged?.Invoke(this, CreateStatusSnapshot());
        _process.Kill();
        return Task.FromResult(true);
    }

    public async Task<int> CleanupLingeringProcessesAsync(string comfyRootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comfyRootPath))
        {
            return 0;
        }

        _pythonPathService.Resolve(comfyRootPath);
        var pythonPath = _pythonPathService.PythonPath;
        var mainPath = ResolveMainPath(comfyRootPath);

        if (string.IsNullOrWhiteSpace(pythonPath) || string.IsNullOrWhiteSpace(mainPath))
        {
            return 0;
        }

        _lastPythonPath = pythonPath;
        _lastMainPath = mainPath;

        return await Task.Run(() =>
        {
            var killed = 0;
            foreach (var process in Process.GetProcessesByName("python"))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    if (!IsTargetComfyPythonProcess(process, pythonPath, mainPath))
                    {
                        continue;
                    }

                    process.Kill();
                    killed++;
                    OutputReceived?.Invoke(this, $"[ProcessService] 启动前已清理残留进程 PID={process.Id}");
                }
                catch (Exception ex)
                {
                    _logService.LogError("启动前清理残留进程失败", ex);
                }
                finally
                {
                    process.Dispose();
                }
            }

            return killed;
        }, cancellationToken);
    }

    public Task<bool> RequestGracefulStopAsync(CancellationToken cancellationToken = default)
    {
        if (_process == null || _process.HasExited)
        {
            return Task.FromResult(false);
        }

        _stopRequestedByUser = true;
        _recoveryDeadlineUtc = null;
        lock (_statusLock)
        {
            _status.State = ProcessState.Stopping;
            _status.IsRunning = true;
        }
        StatusChanged?.Invoke(this, CreateStatusSnapshot());

        if (TrySendConsoleCtrlEvent(CtrlCEvent))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(TrySendConsoleCtrlEvent(CtrlBreakEvent));
    }

    public async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_process == null || _process.HasExited)
        {
            var snapshot = CreateStatusSnapshot();
            return !snapshot.IsRunning;
        }

        if (timeout <= TimeSpan.Zero)
        {
            return _process.HasExited;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        try
        {
            await _process.WaitForExitAsync(linkedCts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return _process.HasExited;
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        StopHeartbeat();
        StopWebSocketMonitor();
        _heartbeatTimer.Dispose();
        _httpClient.Dispose();
        _process?.Dispose();
    }

    private void StartHeartbeat()
    {
        _isHeartbeatEnabled = true;
        _lastHeartbeatSuccess = false;
        _heartbeatTimer.Change(HeartbeatIntervalMs, HeartbeatIntervalMs);
    }

    private void StopHeartbeat()
    {
        _isHeartbeatEnabled = false;
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async void OnHeartbeatTick(object? state)
    {
        if (!_isHeartbeatEnabled)
        {
            return;
        }

        var httpAlive = await CheckHeartbeatAsync();
        var wsAlive = IsWebSocketAlive();
        var combinedAlive = httpAlive || wsAlive;

        if (combinedAlive != _lastHeartbeatSuccess)
        {
            _lastHeartbeatSuccess = combinedAlive;
            HeartbeatStatusChanged?.Invoke(this, combinedAlive);
        }

        EvaluateLiveness(httpAlive, wsAlive);
    }

    private void EvaluateLiveness(bool httpAlive, bool wsAlive)
    {
        var nowUtc = DateTime.UtcNow;
        var combinedAlive = httpAlive || wsAlive;
        var shouldNotify = false;

        lock (_statusLock)
        {
            if (combinedAlive)
            {
                _recoveryDeadlineUtc = null;
                if (!_status.StartTime.HasValue)
                {
                    _status.StartTime = DateTime.Now;
                }

                var processId = _process is { HasExited: false } ? _process.Id : (int?)null;
                if (_status.State != ProcessState.Running || !_status.IsRunning || _status.ProcessId != processId)
                {
                    _status.State = ProcessState.Running;
                    _status.IsRunning = true;
                    _status.ProcessId = processId;
                    shouldNotify = true;
                }
            }
            else
            {
                if (_stopRequestedByUser)
                {
                    if (_status.State != ProcessState.Stopped || _status.IsRunning || _status.ProcessId != null)
                    {
                        _status.State = ProcessState.Stopped;
                        _status.IsRunning = false;
                        _status.ProcessId = null;
                        shouldNotify = true;
                    }
                }
                else if (_process is { HasExited: false })
                {
                    if (_status.State != ProcessState.Starting && _status.State != ProcessState.Recovering)
                    {
                        _status.State = ProcessState.Recovering;
                        _status.IsRunning = true;
                        shouldNotify = true;
                    }
                }
                else
                {
                    if (!_recoveryDeadlineUtc.HasValue)
                    {
                        _recoveryDeadlineUtc = nowUtc.AddMilliseconds(RecoveryWindowMs);
                        _status.State = ProcessState.Recovering;
                        _status.IsRunning = true;
                        _status.ProcessId = null;
                        shouldNotify = true;
                    }
                    else if (nowUtc > _recoveryDeadlineUtc.Value)
                    {
                        if (_status.State != ProcessState.Stopped || _status.IsRunning || _status.ProcessId != null)
                        {
                            _status.State = ProcessState.Stopped;
                            _status.IsRunning = false;
                            _status.ProcessId = null;
                            shouldNotify = true;
                        }
                    }
                }
            }

            if (_status.StartTime.HasValue)
            {
                _status.Uptime = DateTime.Now - _status.StartTime.Value;
            }
        }

        if (shouldNotify)
        {
            var snapshot = CreateStatusSnapshot();
            if (combinedAlive)
            {
                OutputReceived?.Invoke(this, "[ProcessService] 连接恢复：ComfyUI 服务在线");
            }
            else if (_stopRequestedByUser)
            {
                OutputReceived?.Invoke(this, "[ProcessService] 已完成用户请求的停止流程");
                StopHeartbeat();
                StopWebSocketMonitor();
            }
            else
            {
                OutputReceived?.Invoke(this, "[ProcessService] 服务暂不可用，等待恢复中...");
            }

            StatusChanged?.Invoke(this, snapshot);
        }
    }

    private async Task<bool> CheckHeartbeatAsync()
    {
        if (string.IsNullOrWhiteSpace(_comfyApiUrl))
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.GetAsync(_comfyApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var responseText = await response.Content.ReadAsStringAsync();
            UpdateSystemStats(responseText);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private void StartWebSocketMonitor()
    {
        StopWebSocketMonitor();
        _webSocketWaitingForServerNotified = false;
        _webSocketDisconnectedNotified = false;
        _webSocketCts = new CancellationTokenSource();
        _webSocketMonitorTask = Task.Run(() => MonitorWebSocketLoopAsync(_webSocketCts.Token));
    }

    private void StopWebSocketMonitor()
    {
        try
        {
            _webSocketCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        try
        {
            // 避免退出流程卡住：不阻塞等待 WebSocket close 握手
            // 直接释放底层 socket，由取消令牌驱动监控循环退出。
            _webSocket?.Abort();
        }
        catch
        {
            // ignored
        }
        finally
        {
            _webSocket?.Dispose();
            _webSocket = null;
        }

        try
        {
            _webSocketMonitorTask?.Wait(TimeSpan.FromMilliseconds(300));
        }
        catch
        {
            // ignored
        }

        _webSocketMonitorTask = null;
        _webSocketCts?.Dispose();
        _webSocketCts = null;
    }

    private async Task MonitorWebSocketLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(WebSocketInitialConnectDelayMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested && !_isDisposed)
        {
            ClientWebSocket? ws = null;
            var connected = false;
            try
            {
                ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                var clientId = $"comfyshell-{Environment.ProcessId}";
                var wsUri = new Uri($"{_comfyWsUrl}?clientId={clientId}");
                await ws.ConnectAsync(wsUri, cancellationToken);
                connected = true;
                _webSocket = ws;
                OnWebSocketConnected();
                await ReceiveWebSocketMessagesAsync(ws, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex) when (!connected && IsExpectedConnectFailure(ex))
            {
                NotifyWebSocketWaitingForServer(ex.Message);
                EvaluateLiveness(httpAlive: false, wsAlive: false);
            }
            catch (Exception ex)
            {
                if (!connected && IsExpectedConnectFailure(ex))
                {
                    NotifyWebSocketWaitingForServer(ex.Message);
                }
                else
                {
                    _logService.LogError("WebSocket 监控异常", ex);
                }
                OnWebSocketDisconnected();
            }
            finally
            {
                if (_webSocket == ws)
                {
                    _webSocket = null;
                }
                ws?.Dispose();
            }

            try
            {
                await Task.Delay(WebSocketReconnectDelayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReceiveWebSocketMessagesAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var segment = new ArraySegment<byte>(buffer);
            using var ms = new MemoryStream();

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(segment, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    OnWebSocketDisconnected();
                    return;
                }

                if (result.Count > 0)
                {
                    ms.Write(buffer, 0, result.Count);
                }
            }
            while (!result.EndOfMessage);

            if (ms.Length == 0 || result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var message = Encoding.UTF8.GetString(ms.ToArray());
            OnWebSocketMessage(message);
        }
    }

    private void OnWebSocketConnected()
    {
        lock (_statusLock)
        {
            _lastHeartbeatSuccess = true;
        }
        _webSocketWaitingForServerNotified = false;
        _webSocketDisconnectedNotified = false;
        OutputReceived?.Invoke(this, "[ProcessService] WebSocket 已连接");
        HeartbeatStatusChanged?.Invoke(this, true);
        EvaluateLiveness(httpAlive: false, wsAlive: true);
    }

    private void OnWebSocketDisconnected()
    {
        if (!_webSocketDisconnectedNotified)
        {
            _webSocketDisconnectedNotified = true;
            OutputReceived?.Invoke(this, "[ProcessService] WebSocket 已断开，等待重连...");
        }
        EvaluateLiveness(httpAlive: false, wsAlive: false);
    }

    private void OnWebSocketMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // 只要能收到消息就说明服务在线；再按类型输出少量诊断日志避免刷屏
        EvaluateLiveness(httpAlive: false, wsAlive: true);

        try
        {
            using var doc = JsonDocument.Parse(message);
            if (!doc.RootElement.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var messageType = typeElement.GetString();
            if (string.Equals(messageType, "status", StringComparison.OrdinalIgnoreCase))
            {
                OutputReceived?.Invoke(this, "[ProcessService] WebSocket status 消息：服务在线");
            }
        }
        catch (JsonException)
        {
            // 非标准消息直接忽略
        }
    }

    private bool IsWebSocketAlive()
    {
        return _webSocket is { State: WebSocketState.Open };
    }

    private void NotifyWebSocketWaitingForServer(string reason)
    {
        if (_webSocketWaitingForServerNotified)
        {
            return;
        }

        _webSocketWaitingForServerNotified = true;
        _webSocketDisconnectedNotified = false;
        OutputReceived?.Invoke(this, $"[ProcessService] WebSocket 暂不可连接（等待 ComfyUI 启动）：{reason}");
    }

    private static bool IsExpectedConnectFailure(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("Unable to connect to the remote server", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase);
    }

    private ProcessStatus CreateStatusSnapshot()
    {
        lock (_statusLock)
        {
            var snapshot = new ProcessStatus
            {
                VersionId = _status.VersionId,
                IsRunning = _status.IsRunning,
                ProcessId = _status.ProcessId,
                StartTime = _status.StartTime,
                Uptime = _status.StartTime.HasValue ? DateTime.Now - _status.StartTime.Value : null,
                LastError = _status.LastError,
                State = _status.State,
                OutputLogs = new List<string>(_status.OutputLogs)
            };
            return snapshot;
        }
    }

    private void MarkStopped(string reason)
    {
        lock (_statusLock)
        {
            _status.State = ProcessState.Stopped;
            _status.IsRunning = false;
            _status.ProcessId = null;
            if (_status.StartTime.HasValue)
            {
                _status.Uptime = DateTime.Now - _status.StartTime.Value;
            }
        }

        StopHeartbeat();
        StopWebSocketMonitor();
        OutputReceived?.Invoke(this, $"[ProcessService] {reason}");
        HeartbeatStatusChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, CreateStatusSnapshot());
    }

    private void UpdateSystemStats(string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            if (string.Equals(pretty, _lastSystemStats, StringComparison.Ordinal))
            {
                return;
            }

            _lastSystemStats = pretty;
            SystemStatsUpdated?.Invoke(this, pretty);
        }
        catch (JsonException)
        {
            if (string.Equals(responseText, _lastSystemStats, StringComparison.Ordinal))
            {
                return;
            }

            _lastSystemStats = responseText;
            SystemStatsUpdated?.Invoke(this, responseText);
        }
    }

    private bool TrySendConsoleCtrlEvent(uint ctrlEvent)
    {
        if (_process == null || _process.HasExited)
        {
            return false;
        }

        var attached = false;
        try
        {
            attached = AttachConsole((uint)_process.Id);
            if (!attached)
            {
                return false;
            }

            SetConsoleCtrlHandler(IntPtr.Zero, true);
            return GenerateConsoleCtrlEvent(ctrlEvent, 0);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (attached)
            {
                FreeConsole();
                SetConsoleCtrlHandler(IntPtr.Zero, false);
            }
        }
    }

    private ProcessStartInfo? BuildStartInfo(string comfyRootPath, string arguments)
    {
        if (string.IsNullOrWhiteSpace(comfyRootPath))
        {
            return null;
        }

        _pythonPathService.Resolve(comfyRootPath);
        var pythonPath = _pythonPathService.PythonPath;
        var mainPath = ResolveMainPath(comfyRootPath);
        if (string.IsNullOrWhiteSpace(pythonPath) || string.IsNullOrWhiteSpace(mainPath))
        {
            return null;
        }

        _lastPythonPath = pythonPath;
        _lastMainPath = mainPath;

        var argsStr = string.IsNullOrWhiteSpace(arguments) ? "" : $" {arguments}";
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"-s \"{mainPath}\" --windows-standalone-build{argsStr}",
            WorkingDirectory = comfyRootPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true
        };

        _proxyService.ConfigureProcessProxy(startInfo);
        startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        return startInfo;
    }

    private bool TryKillLingeringComfyPythonProcesses()
    {
        if (string.IsNullOrWhiteSpace(_lastPythonPath) || string.IsNullOrWhiteSpace(_lastMainPath))
        {
            return false;
        }

        var killedAny = false;
        foreach (var process in Process.GetProcessesByName("python"))
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                if (!IsTargetComfyPythonProcess(process, _lastPythonPath, _lastMainPath))
                {
                    continue;
                }

                process.Kill();
                killedAny = true;
                OutputReceived?.Invoke(this, $"[ProcessService] 已清理残留 Python 进程 PID={process.Id}");
            }
            catch (Exception ex)
            {
                _logService.LogError("清理残留 Python 进程失败", ex);
            }
            finally
            {
                process.Dispose();
            }
        }

        return killedAny;
    }

    private bool IsTargetComfyPythonProcess(Process process, string pythonPath, string mainPath)
    {
        var processPath = process.MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        if (!string.Equals(processPath, pythonPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commandLine = TryGetProcessCommandLine(process.Id);
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        return commandLine.Contains(mainPath, StringComparison.OrdinalIgnoreCase) &&
               commandLine.Contains("--windows-standalone-build", StringComparison.OrdinalIgnoreCase);
    }

    private string? TryGetProcessCommandLine(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            foreach (var item in searcher.Get())
            {
                using var processObject = (ManagementObject)item;
                return processObject["CommandLine"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"读取进程命令行失败 PID={processId}", ex);
        }

        return null;
    }

    private static string? ResolveMainPath(string rootPath)
    {
        var comfyMain = Path.Combine(rootPath, "ComfyUI", "main.py");
        if (File.Exists(comfyMain))
        {
            return comfyMain;
        }

        var rootMain = Path.Combine(rootPath, "main.py");
        if (File.Exists(rootMain))
        {
            return rootMain;
        }

        return null;
    }
}
