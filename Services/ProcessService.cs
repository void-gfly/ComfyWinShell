using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.WebSockets;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// ComfyUI 进程生命周期与在线状态管理服务。
/// </summary>
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

    /// <summary>
    /// 将当前进程附加到目标控制台。
    /// </summary>
    /// <param name="dwProcessId">目标进程标识。</param>
    /// <returns>附加成功时返回 true，否则返回 false。</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    /// <summary>
    /// 释放当前附加的控制台。
    /// </summary>
    /// <returns>释放成功时返回 true，否则返回 false。</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    /// <summary>
    /// 向控制台进程组发送控制台事件。
    /// </summary>
    /// <param name="dwCtrlEvent">控制台事件类型。</param>
    /// <param name="dwProcessGroupId">目标进程组标识。</param>
    /// <returns>发送成功时返回 true，否则返回 false。</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    /// <summary>
    /// 设置当前进程的控制台信号处理器。
    /// </summary>
    /// <param name="handlerRoutine">处理器函数指针。</param>
    /// <param name="add">是否添加处理器。</param>
    /// <returns>设置成功时返回 true，否则返回 false。</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);

    public event EventHandler<ProcessStatus>? StatusChanged;
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<bool>? HeartbeatStatusChanged;
    public event EventHandler<string>? SystemStatsUpdated;

    /// <summary>
    /// 初始化进程服务。
    /// </summary>
    /// <param name="argumentBuilder">启动参数构建器。</param>
    /// <param name="pythonPathService">Python 路径服务。</param>
    /// <param name="proxyService">代理服务。</param>
    /// <param name="logService">日志服务。</param>
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

    /// <summary>
    /// 配置 HTTP 心跳和 WebSocket 监控使用的 ComfyUI 端点。
    /// </summary>
    /// <param name="listen">监听地址。</param>
    /// <param name="port">监听端口。</param>
    public void ConfigureApiEndpoint(string listen, int port)
    {
        var normalizedListen = listen == "0.0.0.0" ? "127.0.0.1" : listen;
        _comfyApiUrl = $"http://{normalizedListen}:{port}/system_stats";
        _comfyWsUrl = $"ws://{normalizedListen}:{port}/ws";
    }

    /// <summary>
    /// 获取当前进程状态快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>运行中时返回状态对象，否则返回 null。</returns>
    public async Task<ProcessStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var snapshot = CreateStatusSnapshot();
        return snapshot.IsRunning ? snapshot : null;
    }

    /// <summary>
    /// 启动 ComfyUI 进程并建立状态监控。
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录。</param>
    /// <param name="configuration">启动配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>启动成功时返回 true，否则返回 false。</returns>
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

        var existingProcess = FindExistingComfyProcess(_lastPythonPath!, _lastMainPath!);
        if (existingProcess != null)
        {
            OutputReceived?.Invoke(this, $"[ProcessService] 检测到同副本已在运行(PID={existingProcess.Id})，跳过重复启动，转入连接检测。");
            _process = existingProcess;
            _process.EnableRaisingEvents = true;
            _process.Exited -= OnProcessExited;
            _process.Exited += OnProcessExited;

            lock (_statusLock)
            {
                _status = new ProcessStatus
                {
                    VersionId = "local",
                    State = ProcessState.Starting,
                    IsRunning = true,
                    ProcessId = existingProcess.Id,
                    StartTime = DateTime.Now
                };
            }

            StatusChanged?.Invoke(this, CreateStatusSnapshot());
            StartHeartbeat();
            StartWebSocketMonitor();
            return Task.FromResult(true);
        }

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

    /// <summary>
    /// 处理底层进程退出事件，并将状态转入恢复观察流程。
    /// </summary>
    /// <param name="sender">事件发送方。</param>
    /// <param name="e">事件参数。</param>
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

    /// <summary>
    /// 强制停止当前 ComfyUI 进程，并在必要时清理残留 Python 进程。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发送停止或完成清理时返回 true，否则返回 false。</returns>
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

    /// <summary>
    /// 在启动前清理与当前副本匹配的残留 ComfyUI Python 进程。
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>清理掉的进程数量。</returns>
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

    /// <summary>
    /// 尝试向控制台进程发送优雅停止信号。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>信号发送成功时返回 true，否则返回 false。</returns>
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

    /// <summary>
    /// 在指定时间内等待进程退出。
    /// </summary>
    /// <param name="timeout">等待超时时长。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程在超时前退出时返回 true，否则返回 false。</returns>
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

    /// <summary>
    /// 释放进程监控相关资源。
    /// </summary>
    public void Dispose()
    {
        _isDisposed = true;
        StopHeartbeat();
        StopWebSocketMonitor();
        _heartbeatTimer.Dispose();
        _httpClient.Dispose();
        _process?.Dispose();
    }

    /// <summary>
    /// 启动 HTTP 心跳检测定时器。
    /// </summary>
    private void StartHeartbeat()
    {
        _isHeartbeatEnabled = true;
        _lastHeartbeatSuccess = false;
        _heartbeatTimer.Change(HeartbeatIntervalMs, HeartbeatIntervalMs);
    }

    /// <summary>
    /// 停止 HTTP 心跳检测定时器。
    /// </summary>
    private void StopHeartbeat()
    {
        _isHeartbeatEnabled = false;
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// 心跳定时回调，综合 HTTP 与 WebSocket 状态评估服务在线情况。
    /// </summary>
    /// <param name="state">定时器状态对象。</param>
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

    /// <summary>
    /// 根据 HTTP 与 WebSocket 结果更新当前服务存活状态。
    /// </summary>
    /// <param name="httpAlive">HTTP 心跳是否成功。</param>
    /// <param name="wsAlive">WebSocket 是否在线。</param>
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

    /// <summary>
    /// 通过 system_stats 接口执行一次 HTTP 心跳检查。
    /// </summary>
    /// <returns>接口可访问时返回 true，否则返回 false。</returns>
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

    /// <summary>
    /// 启动后台 WebSocket 监控循环。
    /// </summary>
    private void StartWebSocketMonitor()
    {
        StopWebSocketMonitor();
        _webSocketWaitingForServerNotified = false;
        _webSocketDisconnectedNotified = false;
        _webSocketCts = new CancellationTokenSource();
        _webSocketMonitorTask = Task.Run(() => MonitorWebSocketLoopAsync(_webSocketCts.Token));
    }

    /// <summary>
    /// 停止 WebSocket 监控并释放连接资源。
    /// </summary>
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

    /// <summary>
    /// 维持 WebSocket 长连接并在断开后重试重连。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
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

    /// <summary>
    /// 接收 WebSocket 文本消息并转发给状态处理逻辑。
    /// </summary>
    /// <param name="ws">当前 WebSocket 连接。</param>
    /// <param name="cancellationToken">取消令牌。</param>
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

    /// <summary>
    /// 在 WebSocket 成功连接后更新内部状态并通知外部。
    /// </summary>
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

    /// <summary>
    /// 在 WebSocket 断开后更新状态并输出诊断信息。
    /// </summary>
    private void OnWebSocketDisconnected()
    {
        if (!_webSocketDisconnectedNotified)
        {
            _webSocketDisconnectedNotified = true;
            OutputReceived?.Invoke(this, "[ProcessService] WebSocket 已断开，等待重连...");
        }
        EvaluateLiveness(httpAlive: false, wsAlive: false);
    }

    /// <summary>
    /// 处理接收到的 WebSocket 消息。
    /// </summary>
    /// <param name="message">原始消息文本。</param>
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

    /// <summary>
    /// 判断当前 WebSocket 连接是否处于可用状态。
    /// </summary>
    /// <returns>连接处于打开状态时返回 true，否则返回 false。</returns>
    private bool IsWebSocketAlive()
    {
        return _webSocket is { State: WebSocketState.Open };
    }

    /// <summary>
    /// 在首次连接失败时输出等待服务器启动的提示。
    /// </summary>
    /// <param name="reason">连接失败原因。</param>
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

    /// <summary>
    /// 判断异常是否属于预期的连接失败类型。
    /// </summary>
    /// <param name="ex">待分析异常。</param>
    /// <returns>属于预期连接失败时返回 true，否则返回 false。</returns>
    private static bool IsExpectedConnectFailure(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("Unable to connect to the remote server", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建当前进程状态的线程安全快照。
    /// </summary>
    /// <returns>当前状态快照对象。</returns>
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

    /// <summary>
    /// 将状态标记为已停止并广播通知。
    /// </summary>
    /// <param name="reason">停止原因说明。</param>
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

    /// <summary>
    /// 更新缓存的 system_stats 文本，并在内容变化时发送事件。
    /// </summary>
    /// <param name="responseText">接口返回文本。</param>
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

    /// <summary>
    /// 尝试向当前进程发送控制台中断事件。
    /// </summary>
    /// <param name="ctrlEvent">控制台事件类型。</param>
    /// <returns>发送成功时返回 true，否则返回 false。</returns>
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

    /// <summary>
    /// 构建启动 ComfyUI 所需的进程启动信息。
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录。</param>
    /// <param name="arguments">附加命令行参数。</param>
    /// <returns>可用的启动信息；缺少关键路径时返回 null。</returns>
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

    /// <summary>
    /// 清理与最近一次启动配置匹配的残留 Python 进程。
    /// </summary>
    /// <returns>存在并清理成功时返回 true，否则返回 false。</returns>
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

    /// <summary>
    /// 判断指定 Python 进程是否属于当前 ComfyUI 副本。
    /// </summary>
    /// <param name="process">待检查进程。</param>
    /// <param name="pythonPath">目标 Python 可执行文件路径。</param>
    /// <param name="mainPath">目标 main.py 路径。</param>
    /// <returns>匹配时返回 true，否则返回 false。</returns>
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

        return IsMainScriptArgumentMatch(commandLine, mainPath) &&
               commandLine.Contains("--windows-standalone-build", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 查找是否已有相同 Python 与 main.py 组合的 ComfyUI 进程在运行。
    /// </summary>
    /// <param name="pythonPath">目标 Python 可执行文件路径。</param>
    /// <param name="mainPath">目标 main.py 路径。</param>
    /// <returns>找到时返回进程对象，否则返回 null。</returns>
    private Process? FindExistingComfyProcess(string pythonPath, string mainPath)
    {
        foreach (var process in Process.GetProcessesByName("python"))
        {
            var shouldDispose = true;
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                if (IsTargetComfyPythonProcess(process, pythonPath, mainPath))
                {
                    shouldDispose = false;
                    return process;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("检测已运行 ComfyUI 进程失败", ex);
            }
            
            if (shouldDispose)
            {
                process.Dispose();
            }
        }

        return null;
    }

    /// <summary>
    /// 判断命令行中是否包含目标 main.py 路径参数。
    /// </summary>
    /// <param name="commandLine">进程命令行文本。</param>
    /// <param name="mainPath">目标 main.py 路径。</param>
    /// <returns>匹配时返回 true，否则返回 false。</returns>
    private static bool IsMainScriptArgumentMatch(string commandLine, string mainPath)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(mainPath))
        {
            return false;
        }

        // 严格匹配 `-s <main.py>` 参数，允许 main.py 带或不带引号
        var escapedMainPath = Regex.Escape(mainPath);
        var pattern = $@"(?:^|\s)-s\s+""?{escapedMainPath}""?(?:\s|$)";
        return Regex.IsMatch(commandLine, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 读取指定进程的完整命令行。
    /// </summary>
    /// <param name="processId">进程标识。</param>
    /// <returns>命令行文本；读取失败时返回 null。</returns>
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

    /// <summary>
    /// 解析 ComfyUI main.py 的实际路径。
    /// </summary>
    /// <param name="rootPath">ComfyUI 根目录。</param>
    /// <returns>main.py 完整路径；未找到时返回 null。</returns>
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
