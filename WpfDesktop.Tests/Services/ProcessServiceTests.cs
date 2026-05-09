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

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private sealed class FakePythonPathService : IPythonPathService
    {
        public string? PythonPath => null;

        public bool IsValid => false;

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

        public string? PythonPath => Path.Combine(Path.GetTempPath(), "python.exe");

        public bool IsValid => true;

        public void Resolve(string comfyRootPath)
        {
            _resolveEntered.Set();
            _releaseResolve.Wait(TimeSpan.FromSeconds(5));
        }
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
