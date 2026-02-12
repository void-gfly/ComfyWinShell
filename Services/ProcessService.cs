using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
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

    private readonly ArgumentBuilder _argumentBuilder;
    private readonly IPythonPathService _pythonPathService;
    private readonly IProxyService _proxyService;
    private readonly ILogService _logService;
    private readonly HttpClient _httpClient;
    private readonly Timer _heartbeatTimer;
    private Process? _process;
    private ProcessStatus _status = new();
    private string _comfyApiUrl = "http://127.0.0.1:8188/system_stats";
    private string? _lastPythonPath;
    private string? _lastMainPath;
    private string _lastSystemStats = "暂无 ComfyUI system_stats 数据";
    private bool _isHeartbeatEnabled;
    private bool _lastHeartbeatSuccess;

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
    }

    public async Task<ProcessStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var apiAlive = await CheckHeartbeatAsync();
        if (apiAlive)
        {
            _status.State = ProcessState.Running;
            _status.IsRunning = true;
            if (!_status.StartTime.HasValue)
            {
                _status.StartTime = DateTime.Now;
            }

            if (_status.StartTime.HasValue)
            {
                _status.Uptime = DateTime.Now - _status.StartTime.Value;
            }

            return _status;
        }

        if (_process is { HasExited: false })
        {
            if (_status.StartTime.HasValue)
            {
                _status.Uptime = DateTime.Now - _status.StartTime.Value;
            }

            return _status;
        }

        _status.State = ProcessState.Stopped;
        _status.IsRunning = false;
        return null;
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
        StatusChanged?.Invoke(this, _status);

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _status.OutputLogs.Add(e.Data);
                OutputReceived?.Invoke(this, e.Data);
            }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _status.OutputLogs.Add(e.Data);
                OutputReceived?.Invoke(this, e.Data);
            }
        };

        _process.Exited += OnProcessExited;

        var started = _process.Start();
        if (started)
        {
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _status.State = ProcessState.Running;
            _status.StartTime = DateTime.Now;
            _status.ProcessId = _process.Id;
            StatusChanged?.Invoke(this, _status);
            StartHeartbeat();
        }
        else
        {
            _status.State = ProcessState.Error;
            _status.IsRunning = false;
            StatusChanged?.Invoke(this, _status);
        }

        return Task.FromResult(started);
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        OutputReceived?.Invoke(this, "[ProcessService] 检测到进程退出，正在检查服务状态...");

        Task.Run(async () =>
        {
            await Task.Delay(2000);
            var isAlive = await CheckHeartbeatAsync();

            if (isAlive)
            {
                OutputReceived?.Invoke(this, "[ProcessService] 检测到 ComfyUI 内部重启，服务仍在运行");
                _status.State = ProcessState.Running;
                _status.IsRunning = true;
                _status.ProcessId = null;
            }
            else
            {
                _status.State = ProcessState.Stopped;
                _status.IsRunning = false;
                if (_status.StartTime.HasValue)
                {
                    _status.Uptime = DateTime.Now - _status.StartTime.Value;
                }

                StopHeartbeat();
            }

            StatusChanged?.Invoke(this, _status);
        });
    }

    public Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        StopHeartbeat();

        if (_process == null || _process.HasExited)
        {
            var killed = TryKillLingeringComfyPythonProcesses();
            _status.State = ProcessState.Stopped;
            _status.IsRunning = false;
            StatusChanged?.Invoke(this, _status);
            return Task.FromResult(killed);
        }

        _status.State = ProcessState.Stopping;
        StatusChanged?.Invoke(this, _status);
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

        _status.State = ProcessState.Stopping;
        StatusChanged?.Invoke(this, _status);

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
            return true;
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
        StopHeartbeat();
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

        var isAlive = await CheckHeartbeatAsync();
        if (isAlive != _lastHeartbeatSuccess)
        {
            _lastHeartbeatSuccess = isAlive;
            HeartbeatStatusChanged?.Invoke(this, isAlive);

            if (isAlive && _status.State != ProcessState.Running)
            {
                OutputReceived?.Invoke(this, "[ProcessService] 心跳检测：ComfyUI 服务已就绪");
                _status.State = ProcessState.Running;
                _status.IsRunning = true;
                StatusChanged?.Invoke(this, _status);
            }
            else if (!isAlive && _status.State == ProcessState.Running)
            {
                OutputReceived?.Invoke(this, "[ProcessService] 心跳检测：ComfyUI 服务暂时不可用，可能正在重启...");
            }
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
