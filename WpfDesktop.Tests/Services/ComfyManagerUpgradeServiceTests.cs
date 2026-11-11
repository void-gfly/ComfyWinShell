using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ComfyManagerUpgradeServiceTests
{
    [Fact]
    public async Task UpgradeAsync_RunsPipInstallInCurrentComfyPython()
    {
        var pythonPath = Path.Combine(Path.GetTempPath(), "python_embeded", "python.exe");
        var comfyRoot = Path.Combine(Path.GetTempPath(), "ComfyUI_windows_portable");
        var runner = new FakeProcessRunner();
        var service = new ComfyManagerUpgradeService(
            new FakeComfyPathService(comfyRoot),
            new FakePythonPathService(pythonPath),
            runner,
            new FakeLogService());

        var result = await service.UpgradeAsync();

        Assert.True(result.Success);
        var command = Assert.Single(runner.Commands);
        Assert.Equal(pythonPath, command.FileName);
        Assert.Equal("-m pip install -U comfyui-manager", command.Arguments);
        Assert.Equal(comfyRoot, command.WorkingDirectory);
    }

    [Fact]
    public async Task UpgradeAsync_WhenPipFails_ReturnsPipOutput()
    {
        var runner = new FakeProcessRunner { ExitCode = 1, StandardError = "no matching distribution" };
        var service = new ComfyManagerUpgradeService(
            new FakeComfyPathService(@"E:\ComfyUI_windows_portable"),
            new FakePythonPathService(@"E:\ComfyUI_windows_portable\python_embeded\python.exe"),
            runner,
            new FakeLogService());

        var result = await service.UpgradeAsync();

        Assert.False(result.Success);
        Assert.Contains("no matching distribution", result.ErrorMessage);
    }

    [Fact]
    public async Task UpgradeAsync_WhenPythonMissing_DoesNotRunPip()
    {
        var runner = new FakeProcessRunner();
        var service = new ComfyManagerUpgradeService(
            new FakeComfyPathService(@"E:\ComfyUI_windows_portable"),
            new FakePythonPathService(null),
            runner,
            new FakeLogService());

        var result = await service.UpgradeAsync();

        Assert.False(result.Success);
        Assert.Contains("Python", result.ErrorMessage);
        Assert.Empty(runner.Commands);
    }

    private sealed class FakeProcessRunner : IExternalProcessRunner
    {
        public List<ExternalProcessCommand> Commands { get; } = new();
        public int ExitCode { get; init; }
        public string StandardError { get; init; } = string.Empty;

        public Task<ExternalProcessResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(new ExternalProcessResult(ExitCode, string.Empty, StandardError));
        }
    }

    private sealed class FakeComfyPathService : IComfyPathService
    {
        public FakeComfyPathService(string comfyRootPath)
        {
            ComfyRootPath = comfyRootPath;
        }

        public string? ComfyUiPath => Path.Combine(ComfyRootPath!, "ComfyUI");
        public string? ComfyRootPath { get; }
        public bool IsValid => true;
        public string? ErrorMessage => null;

        public void Refresh()
        {
        }
    }

    private sealed class FakePythonPathService : IPythonPathService
    {
        public FakePythonPathService(string? pythonPath)
        {
            PythonPath = pythonPath;
        }

        public string? PythonPath { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(PythonPath);

        public void Resolve(string comfyRootPath)
        {
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
}
