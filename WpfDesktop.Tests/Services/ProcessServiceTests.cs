using System.Diagnostics;
using System.Reflection;
using System.Threading;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ProcessServiceTests
{
    [Theory]
    [InlineData("0.0.0.0", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    [InlineData("0.0.0.0,::", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    [InlineData("::", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    [InlineData("127.0.0.1", "http://127.0.0.1:8188/system_stats", "ws://127.0.0.1:8188/ws")]
    public void ConfigureApiEndpoint_NormalizesAllInterfaceListenAddress(
        string listen,
        string expectedApiUrl,
        string expectedWsUrl)
    {
        using var service = new ProcessService(
            new ArgumentBuilder(),
            new FakePythonPathService(),
            new FakeProxyService(),
            new FakeLogService());

        service.ConfigureApiEndpoint(listen, 8188);

        Assert.Equal(expectedApiUrl, GetPrivateField<string>(service, "_comfyApiUrl"));
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
            new FakeLogService());

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
            new FakeLogService());

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
    public void BuildStartInfo_DoesNotForceUtf8PythonEnvironment_ForComfyUiLaunch()
    {
        using var tempRoot = new TempComfyRoot();
        var comfyCorePath = Path.Combine(tempRoot.RootPath, "ComfyUI");
        Directory.CreateDirectory(comfyCorePath);
        File.WriteAllText(Path.Combine(comfyCorePath, "main.py"), "print('ok')");

        using var service = new ProcessService(
            new ArgumentBuilder(),
            new FakePythonPathService(@"C:\ComfyShell\ComfyUI\python_embeded\python.exe"),
            new FakeProxyService(),
            new FakeLogService());

        var startInfo = InvokeBuildStartInfo(service, tempRoot.RootPath, string.Empty);

        Assert.NotNull(startInfo);
        Assert.False(startInfo!.EnvironmentVariables.ContainsKey("PYTHONUTF8"));
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("PYTHONIOENCODING"));
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

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static ProcessStartInfo? InvokeBuildStartInfo(ProcessService service, string comfyRootPath, string arguments)
    {
        var method = typeof(ProcessService).GetMethod("BuildStartInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return method!.Invoke(service, new object[] { comfyRootPath, arguments }) as ProcessStartInfo;
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
}
