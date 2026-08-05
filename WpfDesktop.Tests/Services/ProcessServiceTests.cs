using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ProcessServiceTests
{
    [Theory]
    [InlineData("0.0.0.0", "http://127.0.0.1:8188/queue", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    [InlineData("0.0.0.0,::", "http://127.0.0.1:8188/queue", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    [InlineData("::", "http://127.0.0.1:8188/queue", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    [InlineData("127.0.0.1", "http://127.0.0.1:8188/queue", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    public void ConfigureApiEndpoint_NormalizesAllInterfaceListenAddress(
        string listen,
        string expectedHeartbeatUrl,
        string expectedSystemStatsUrl,
        string expectedWsUrl)
    {
        using var service = new ProcessService(
            new ArgumentBuilder(),
            new FakePythonPathService(),
            new FakeProxyService(),
            new FakeLogService(),
            new FakeSettingsService(),
            new ResiliencePolicyService(new FakeLogService()));

        service.ConfigureApiEndpoint(listen, 8188);

        Assert.Equal(expectedHeartbeatUrl, GetPrivateField<string>(service, "_comfyHeartbeatUrl"));
        Assert.Equal(expectedSystemStatsUrl, GetPrivateField<string>(service, "_comfySystemStatsUrl"));
        Assert.Equal(expectedWsUrl, GetPrivateField<string>(service, "_comfyWsUrl"));
    }

    [Fact]
    public async Task StartAsync_ReturnsTaskBeforePythonResolutionCompletes()
    {
        using var tempRoot = new TempComfyRoot();
        using var resolveEntered = new ManualResetEventSlim(false);
        using var releaseResolve = new ManualResetEventSlim(false);

        var pythonPathService = new BlockingPythonPathService(resolveEntered, releaseResolve);
        using var service = new ProcessService(
            new ArgumentBuilder(),
            pythonPathService,
            new FakeProxyService(),
            new FakeLogService(),
            new FakeSettingsService(),
            new ResiliencePolicyService(new FakeLogService()));

        var startTask = Task.Factory.StartNew(
            () => service.StartAsync(tempRoot.RootPath, new ComfyConfiguration()),
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);

        Assert.True(resolveEntered.Wait(TimeSpan.FromSeconds(1)));
        Assert.Same(startTask, await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromMilliseconds(200))));

        releaseResolve.Set();

        var innerTask = await startTask;
        var started = await innerTask;

        Assert.False(started);
    }

    [Fact]
    public async Task StartAsync_CreatesDefaultAndConfiguredInputOutputTempDirectoriesBeforePythonResolutionCompletes()
    {
        using var tempRoot = new TempComfyRoot();
        var comfyCorePath = Path.Combine(tempRoot.RootPath, "ComfyUI");
        Directory.CreateDirectory(comfyCorePath);
        File.WriteAllText(Path.Combine(comfyCorePath, "main.py"), "print('ok')");

        var customInputDirectory = Path.Combine(tempRoot.RootPath, "custom", "input");
        var customOutputDirectory = Path.Combine(tempRoot.RootPath, "custom", "output");
        var customTempDirectory = Path.Combine(tempRoot.RootPath, "custom", "temp");

        using var resolveEntered = new ManualResetEventSlim(false);
        using var releaseResolve = new ManualResetEventSlim(false);

        var pythonPathService = new BlockingPythonPathService(resolveEntered, releaseResolve);
        using var service = new ProcessService(
            new ArgumentBuilder(),
            pythonPathService,
            new FakeProxyService(),
            new FakeLogService(),
            new FakeSettingsService(),
            new ResiliencePolicyService(new FakeLogService()));

        var configuration = new ComfyConfiguration
        {
            Paths =
            {
                InputDirectory = customInputDirectory,
                OutputDirectory = customOutputDirectory,
                TempDirectory = customTempDirectory
            }
        };

        var startTask = Task.Factory.StartNew(
            () => service.StartAsync(tempRoot.RootPath, configuration),
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);

        Assert.True(resolveEntered.Wait(TimeSpan.FromSeconds(1)));

        var defaultDirectory = comfyCorePath;
        Assert.True(Directory.Exists(Path.Combine(defaultDirectory, "input")));
        Assert.True(Directory.Exists(Path.Combine(defaultDirectory, "output")));
        Assert.True(Directory.Exists(Path.Combine(defaultDirectory, "temp")));
        Assert.True(Directory.Exists(customInputDirectory));
        Assert.True(Directory.Exists(customOutputDirectory));
        Assert.True(Directory.Exists(customTempDirectory));

        releaseResolve.Set();

        var innerTask = await startTask;
        var started = await innerTask;

        Assert.False(started);
    }

    [Fact]
    public void AreSameExecutablePath_ReturnsTrue_ForSameFileWithDifferentFormatting()
    {
        var pythonPath = @"C:\ComfyShell\ComfyUI\python_embeded\python.exe";
        var quotedPath = $"\"{pythonPath}\"";

        var matched = ProcessService.AreSameExecutablePath(quotedPath, pythonPath);

        Assert.True(matched);
    }

    [Fact]
    public void BuildStartInfo_UsesUtf8StandardIoWithoutEnablingPythonUtf8Mode_ForComfyUiLaunch()
    {
        using var tempRoot = new TempComfyRoot();
        var comfyCorePath = Path.Combine(tempRoot.RootPath, "ComfyUI");
        Directory.CreateDirectory(comfyCorePath);
        File.WriteAllText(Path.Combine(comfyCorePath, "main.py"), "print('ok')");

        using var service = new ProcessService(
            new ArgumentBuilder(),
            new FakePythonPathService(@"C:\ComfyShell\ComfyUI\python_embeded\python.exe"),
            new FakeProxyService(),
            new FakeLogService(),
            new FakeSettingsService(),
            new ResiliencePolicyService(new FakeLogService()));

        var startInfo = InvokeBuildStartInfo(service, tempRoot.RootPath, string.Empty, false);

        Assert.NotNull(startInfo);
        Assert.False(startInfo!.EnvironmentVariables.ContainsKey("PYTHONUTF8"));
        Assert.Equal("utf-8", startInfo.EnvironmentVariables["PYTHONIOENCODING"]);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
    }

    [Fact]
    public void BuildStartInfo_DisablesOutputRedirection_ForExternalLaunchMode()
    {
        using var tempRoot = new TempComfyRoot();
        var comfyCorePath = Path.Combine(tempRoot.RootPath, "ComfyUI");
        Directory.CreateDirectory(comfyCorePath);
        File.WriteAllText(Path.Combine(comfyCorePath, "main.py"), "print('ok')");

        var settingsService = new FakeSettingsService(new AppSettings
        {
            ExternalLaunchComfyUI = true
        });

        using var service = new ProcessService(
            new ArgumentBuilder(),
            new FakePythonPathService(@"C:\ComfyShell\ComfyUI\python_embeded\python.exe"),
            new FakeProxyService(),
            new FakeLogService(),
            settingsService,
            new ResiliencePolicyService(new FakeLogService()));

        var startInfo = InvokeBuildStartInfo(service, tempRoot.RootPath, string.Empty, true);

        Assert.NotNull(startInfo);
        Assert.False(startInfo!.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
        Assert.False(startInfo.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Normal, startInfo.WindowStyle);
    }

    [Fact]
    public void CreateDisableExecutionSpeedThrottlingState_ClearsEcoQosExecutionSpeedPolicy()
    {
        var state = ProcessPowerModeService.CreateDisableExecutionSpeedThrottlingState();

        Assert.Equal(1u, state.Version);
        Assert.Equal(ProcessPowerModeService.ProcessPowerThrottlingExecutionSpeed, state.ControlMask);
        Assert.Equal(0u, state.StateMask);
    }

    [Theory]
    [InlineData(ProcessPriorityClass.Idle, true)]
    [InlineData(ProcessPriorityClass.BelowNormal, true)]
    [InlineData(ProcessPriorityClass.Normal, false)]
    [InlineData(ProcessPriorityClass.AboveNormal, false)]
    [InlineData(ProcessPriorityClass.High, false)]
    public void ShouldRestoreNormalPriority_ReturnsTrueOnlyForEfficiencyModePriority(ProcessPriorityClass priorityClass, bool expected)
    {
        Assert.Equal(expected, ProcessPowerModeService.ShouldRestoreNormalPriority(priorityClass));
    }

    [Fact]
    public void ProcessModeBackgroundEnd_UsesWindowsBackgroundModeEndFlag()
    {
        Assert.Equal(0x00200000u, ProcessPowerModeService.ProcessModeBackgroundEnd);
    }

    [Fact]
    public void DecodeComfyOutputBytes_PrefersUtf8_ForUnicodeProgressBars()
    {
        var bytes = Encoding.UTF8.GetBytes(" 50%|██▌     | 2/4");

        var decoded = ProcessService.DecodeComfyOutputBytes(bytes);

        Assert.Equal(" 50%|██▌     | 2/4", decoded);
    }

    [Fact]
    public void DecodeComfyOutputBytes_FallsBackToCurrentAnsiCodePage_ForLocalProcessOutput()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansiEncoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        var bytes = ansiEncoding.GetBytes("远程主机强迫关闭了一个现有的连接。");

        var decoded = ProcessService.DecodeComfyOutputBytes(bytes);

        Assert.Equal("远程主机强迫关闭了一个现有的连接。", decoded);
    }

    [Fact]
    public void DecodeComfyOutputBytes_RepairsUtf8TextAlreadyMojibakedAsAnsi_ForProgressBars()
    {
        var bytes = Encoding.UTF8.GetBytes(" 50%|鈻堚枅鈻堚枅鈻     | 2/4");

        var decoded = ProcessService.DecodeComfyOutputBytes(bytes);

        Assert.Equal(" 50%|█████     | 2/4", decoded);
    }

    [Fact]
    public void DecodeComfyOutputBytes_RepairsUtf8TextAlreadyMojibakedAsAnsi_ForBoxDrawingIcons()
    {
        var bytes = Encoding.UTF8.GetBytes("[06:26:30.157] 鈿     鈹斺攢 VAE weights loaded");

        var decoded = ProcessService.DecodeComfyOutputBytes(bytes);

        Assert.Equal("[06:26:30.157] ⚠     └─ VAE weights loaded", decoded);
    }

    [Theory]
    [InlineData(@"C:\ComfyShell\ComfyUI\python_embeded\python.exe", @"C:\Other\ComfyUI\python_embeded\python.exe")]
    [InlineData(@"C:\ComfyShell\ComfyUI\python_embeded\python.exe", @"C:\ComfyShell\ComfyUI\python.exe")]
    public void IsTargetComfyPythonProcess_ReturnsFalse_WhenExecutablePathDiffers(
        string processPath,
        string pythonPath)
    {
        var matched = ProcessService.IsTargetComfyPythonProcess(processPath, pythonPath);

        Assert.False(matched);
    }

    [Fact]
    public void ProbeFailureWarning_IsSuppressedUntilHeartbeatThreshold()
    {
        var logService = new RecordingLogService();
        using var service = CreateProbeService(logService);

        for (var i = 0; i < 9; i++)
        {
            InvokePrivateVoidMethod(service, "ReportHeartbeatProbeFailure", "超时（10000 毫秒）");
        }

        Assert.Empty(logService.Entries);

        InvokePrivateVoidMethod(service, "ReportHeartbeatProbeFailure", "超时（10000 毫秒）");

        Assert.Single(logService.Entries);
        Assert.Contains(logService.Entries, entry =>
            entry.Level == GUILogLevel.Warning &&
            entry.Message.Contains("HTTP 心跳探测连续失败 10 次", StringComparison.Ordinal));
    }

    [Fact]
    public void ProbeFailureWarning_IsSuppressedWhenSoftAliveFromRecentWebSocketActivity()
    {
        var logService = new RecordingLogService();
        using var service = CreateProbeService(logService);

        SetPrivateField(service, "_lastWebSocketActivityUtc", DateTime.UtcNow);

        for (var i = 0; i < 30; i++)
        {
            InvokePrivateVoidMethod(service, "ReportHeartbeatProbeFailure", "超时（10000 毫秒）");
        }

        Assert.Empty(logService.Entries);
    }

    [Fact]
    public void ProbeFailureWarning_IsNotSuppressedWhenWebSocketActivityIsStale()
    {
        var logService = new RecordingLogService();
        using var service = CreateProbeService(logService);

        SetPrivateField(service, "_lastWebSocketActivityUtc", DateTime.UtcNow.AddMinutes(-5));

        for (var i = 0; i < 10; i++)
        {
            InvokePrivateVoidMethod(service, "ReportHeartbeatProbeFailure", "超时（10000 毫秒）");
        }

        Assert.Single(logService.Entries);
        Assert.Contains(logService.Entries, entry =>
            entry.Level == GUILogLevel.Warning &&
            entry.Message.Contains("HTTP 心跳探测连续失败 10 次", StringComparison.Ordinal));
    }

    [Fact]
    public void MarkWebSocketActivity_SetsSoftAliveWindow()
    {
        using var service = CreateProbeService(new FakeLogService());

        Assert.False(InvokePrivateBoolMethod(service, "IsSoftAlive"));

        InvokePrivateVoidMethod(service, "MarkWebSocketActivity");

        Assert.True(InvokePrivateBoolMethod(service, "IsSoftAlive"));
        Assert.NotNull(GetPrivateField<DateTime?>(service, "_lastWebSocketActivityUtc"));
    }

    [Fact]
    public void ProbeFailureWarning_IsIndependent_ForWebSocketDisposed()
    {
        var logService = new RecordingLogService();
        using var service = CreateProbeService(logService);

        for (var i = 0; i < 9; i++)
        {
            InvokePrivateVoidMethod(service, "ReportWebSocketDisposedProbeFailure", "Cannot access a disposed object.");
        }

        Assert.Empty(logService.Entries);

        InvokePrivateVoidMethod(service, "ReportWebSocketDisposedProbeFailure", "Cannot access a disposed object.");

        Assert.Single(logService.Entries);
        Assert.Contains(logService.Entries, entry =>
            entry.Level == GUILogLevel.Warning &&
            entry.Message.Contains("WebSocket 探测异常连续出现 10 次", StringComparison.Ordinal));
    }

    [Fact]
    public void ProbeFailureWarning_IsClearedWhenWebSocketConnectSucceeds()
    {
        var logService = new RecordingLogService();
        using var service = CreateProbeService(logService);

        for (var i = 0; i < 9; i++)
        {
            InvokePrivateVoidMethod(service, "ReportWebSocketConnectProbeFailure", "Unable to connect to the remote server");
        }

        Assert.DoesNotContain(logService.Entries, entry =>
            entry.Level == GUILogLevel.Warning &&
            entry.Message.Contains("WebSocket 连接探测连续失败", StringComparison.Ordinal));

        InvokePrivateVoidMethod(service, "OnWebSocketConnected");

        InvokePrivateVoidMethod(service, "ReportWebSocketConnectProbeFailure", "Unable to connect to the remote server");

        Assert.DoesNotContain(logService.Entries, entry =>
            entry.Level == GUILogLevel.Warning &&
            entry.Message.Contains("WebSocket 连接探测连续失败", StringComparison.Ordinal));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void InvokePrivateVoidMethod(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, args);
    }

    private static bool InvokePrivateBoolMethod(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(instance, args));
    }

    private static ProcessService CreateProbeService(ILogService logService)
    {
        return new ProcessService(
            new ArgumentBuilder(),
            new FakePythonPathService(),
            new FakeProxyService(),
            logService,
            new FakeSettingsService(),
            new ResiliencePolicyService(new FakeLogService()));
    }

    private static ProcessStartInfo? InvokeBuildStartInfo(ProcessService service, string comfyRootPath, string arguments, bool externalLaunchComfyUI)
    {
        var method = typeof(ProcessService).GetMethod("BuildStartInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return method!.Invoke(service, new object[] { comfyRootPath, arguments, externalLaunchComfyUI }) as ProcessStartInfo;
    }

    private sealed class FakePythonPathService : IPythonPathService
    {
        public string? PythonPath { get; }

        public bool IsValid => false;

        public FakePythonPathService(string? pythonPath = null)
        {
            PythonPath = pythonPath;
        }

        public void Resolve(string comfyRootPath)
        {
        }
    }

    private sealed class BlockingPythonPathService : IPythonPathService
    {
        private readonly ManualResetEventSlim _resolveEntered;
        private readonly ManualResetEventSlim _releaseResolve;

        public BlockingPythonPathService(ManualResetEventSlim resolveEntered, ManualResetEventSlim releaseResolve)
        {
            _resolveEntered = resolveEntered;
            _releaseResolve = releaseResolve;
        }

        public bool IsValid => true;

        public void Resolve(string comfyRootPath)
        {
            _resolveEntered.Set();
            _releaseResolve.Wait(TimeSpan.FromSeconds(5));
        }

        public string? PythonPath => null;
    }

    private sealed class TempComfyRoot : IDisposable
    {
        public string RootPath { get; }

        public TempComfyRoot()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "WpfDesktopTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class FakeProxyService : IProxyService
    {
        public bool IsEnabled => false;

        public bool IsGitHubMirrorEnabled => false;

        public bool IsPipMirrorEnabled => false;

        public string GetProxyServer() => string.Empty;

        public void ConfigureProcessProxy(System.Diagnostics.ProcessStartInfo startInfo)
        {
        }

        public string ConvertGitHubUrl(string originalUrl) => originalUrl;

        public string GetPipMirrorArgs() => string.Empty;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public FakeSettingsService(AppSettings? settings = null)
        {
            Current = settings ?? new AppSettings();
        }

        public AppSettings Current { get; private set; }

        public Task<AppSettings> LoadAsync()
        {
            return Task.FromResult(Current);
        }

        public Task SaveAsync(AppSettings settings)
        {
            Current = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogService : ILogService
    {
        public event EventHandler<string>? LogReceived;

        public event EventHandler<LogEntry>? LogEntryReceived;

        public void Log(string message)
        {
        }

        public void Log(string message, GUILogLevel level)
        {
        }

        public void LogError(string message, Exception? exception = null)
        {
        }
    }

    private sealed class RecordingLogService : ILogService
    {
        public event EventHandler<string>? LogReceived;

        public event EventHandler<LogEntry>? LogEntryReceived;

        public List<LogRecord> Entries { get; } = new();

        public void Log(string message)
        {
            Log(message, GUILogLevel.Info);
        }

        public void Log(string message, GUILogLevel level)
        {
            Entries.Add(new LogRecord(message, level));
            LogReceived?.Invoke(this, message);
            LogEntryReceived?.Invoke(this, new LogEntry { Message = message, Level = level, Timestamp = DateTime.Now });
        }

        public void LogError(string message, Exception? exception = null)
        {
            Entries.Add(new LogRecord(exception == null ? message : $"{message}: {exception.Message}", GUILogLevel.Error));
        }
    }

    private sealed record LogRecord(string Message, GUILogLevel Level);
}
