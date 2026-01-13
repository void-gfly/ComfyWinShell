using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
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
    private readonly HttpClient _httpClient;
    private readonly Timer _heartbeatTimer;
    private Process? _process;
    private ProcessStatus _status = new();
    private string? _comfyApiUrl;
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

    public ProcessService(ArgumentBuilder argumentBuilder, IPythonPathService pythonPathService)
    {
        _argumentBuilder = argumentBuilder;
        _pythonPathService = pythonPathService;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(HeartbeatTimeoutMs) };
        _heartbeatTimer = new Timer(OnHeartbeatTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public Task<ProcessStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_process == null || _process.HasExited)
        {
            return Task.FromResult<ProcessStatus?>(null);
        }

        if (_status.StartTime.HasValue)
        {
            _status.Uptime = DateTime.Now - _status.StartTime.Value;
        }

        return Task.FromResult<ProcessStatus?>(_status);
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

        // 记录 API 地址用于心跳检测
        var listen = configuration.Network.Listen;
        var port = configuration.Network.Port;
        _comfyApiUrl = $"http://{(listen == "0.0.0.0" ? "127.0.0.1" : listen)}:{port}/system_stats";

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

            // 启动心跳检测
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
        // 进程退出时，不立即更新状态为Stopped
        // 而是等待心跳检测确认服务是否真的停止了（可能是内部重启）
        OutputReceived?.Invoke(this, "[ProcessService] 检测到进程退出，正在检查服务状态...");

        // 延迟一段时间后检查，给内部重启留出时间
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            var isAlive = await CheckHeartbeatAsync();

            if (isAlive)
            {
                // 服务仍在运行，说明是内部重启
                OutputReceived?.Invoke(this, "[ProcessService] 检测到 ComfyUI 内部重启，服务仍在运行");
                _status.State = ProcessState.Running;
                _status.IsRunning = true;
                // 进程ID可能已变化，清除旧的
                _status.ProcessId = null;
            }
            else
            {
                // 服务确实停止了
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
            // 即使我们的进程对象无效，也尝试通过状态重置
            _status.State = ProcessState.Stopped;
            _status.IsRunning = false;
            StatusChanged?.Invoke(this, _status);
            return Task.FromResult(false);
        }

        _status.State = ProcessState.Stopping;
        StatusChanged?.Invoke(this, _status);

        _process.Kill(true);
        return Task.FromResult(true);
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
        if (_process == null)
        {
            return true;
        }

        if (_process.HasExited)
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
            return;

        var isAlive = await CheckHeartbeatAsync();

        // 检测状态变化
        if (isAlive != _lastHeartbeatSuccess)
        {
            _lastHeartbeatSuccess = isAlive;

            if (isAlive && _status.State != ProcessState.Running)
            {
                // 服务恢复运行（可能是内部重启完成）
                OutputReceived?.Invoke(this, "[ProcessService] 心跳检测：ComfyUI 服务已就绪");
                _status.State = ProcessState.Running;
                _status.IsRunning = true;
                StatusChanged?.Invoke(this, _status);
            }
            else if (!isAlive && _status.State == ProcessState.Running)
            {
                // 服务暂时不可用（可能正在内部重启）
                OutputReceived?.Invoke(this, "[ProcessService] 心跳检测：ComfyUI 服务暂时不可用，可能正在重启...");
            }
        }
    }

    private async Task<bool> CheckHeartbeatAsync()
    {
        if (string.IsNullOrEmpty(_comfyApiUrl))
            return false;

        try
        {
            using var response = await _httpClient.GetAsync(_comfyApiUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
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
        catch (Exception)
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

        // 始终使用 Python 直接启动（与 Dashboard 的“启动命令预览”保持一致）。
        // 不再优先/依赖 run*.bat 脚本，避免 cmd/bat 编码与环境差异导致的异常。
        _pythonPathService.Resolve(comfyRootPath);
        var pythonPath = _pythonPathService.PythonPath;
        var mainPath = ResolveMainPath(comfyRootPath);
        if (string.IsNullOrWhiteSpace(pythonPath) || string.IsNullOrWhiteSpace(mainPath))
        {
            return null;
        }

        var argsStr = string.IsNullOrWhiteSpace(arguments) ? "" : $" {arguments}";
        return new ProcessStartInfo
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
    }


    private static string? ResolveMainPath(string rootPath)
    {
        // 便携版: rootPath/ComfyUI/main.py
        var comfyMain = Path.Combine(rootPath, "ComfyUI", "main.py");
        if (File.Exists(comfyMain))
        {
            return comfyMain;
        }

        // 直接克隆版: rootPath/main.py (此时 rootPath 就是 ComfyUI 目录)
        var rootMain = Path.Combine(rootPath, "main.py");
        if (File.Exists(rootMain))
        {
            return rootMain;
        }

        return null;
    }
}
