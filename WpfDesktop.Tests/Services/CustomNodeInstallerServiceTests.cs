using System.Diagnostics;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class CustomNodeInstallerServiceTests
{
    [Fact]
    public async Task InstallAsync_ClonesRequirementsAndInstallPyInOrder()
    {
        using var temp = new TempComfyInstallRoot();
        var runner = new FakeProcessRunner();
        runner.AfterCommand = command =>
        {
            if (command.FileName == "git")
            {
                Directory.CreateDirectory(temp.NodePath);
                File.WriteAllText(Path.Combine(temp.NodePath, "requirements.txt"), "requests");
                File.WriteAllText(Path.Combine(temp.NodePath, "install.py"), "print('install')");
            }
        };

        var service = CreateService(temp, runner);

        var result = await service.InstallAsync("https://github.com/acme/ComfyUI-TestNode.git");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, runner.Commands.Count);
        Assert.Equal("git", runner.Commands[0].FileName);
        Assert.Contains("clone", runner.Commands[0].Arguments);
        Assert.Contains("https://mirror.local/https://github.com/acme/ComfyUI-TestNode.git", runner.Commands[0].Arguments);
        Assert.Equal(temp.CustomNodesPath, runner.Commands[0].WorkingDirectory);
        Assert.EndsWith("python.exe", runner.Commands[1].FileName);
        Assert.Contains("-m pip install -r", runner.Commands[1].Arguments);
        Assert.Equal(temp.ComfyRootPath, runner.Commands[1].WorkingDirectory);
        Assert.EndsWith("python.exe", runner.Commands[2].FileName);
        Assert.Equal("install.py", runner.Commands[2].Arguments);
        Assert.Equal(temp.NodePath, runner.Commands[2].WorkingDirectory);
    }

    [Fact]
    public async Task InstallAsync_ChecksOutTreeReferenceAfterClone()
    {
        using var temp = new TempComfyInstallRoot();
        var runner = new FakeProcessRunner();
        runner.AfterCommand = command =>
        {
            if (command.FileName == "git" && command.Arguments.StartsWith("clone", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(temp.NodePath);
            }
        };

        var service = CreateService(temp, runner);

        var result = await service.InstallAsync("https://github.com/acme/ComfyUI-TestNode/tree/v1.2.3");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Contains("clone", runner.Commands[0].Arguments);
        Assert.Contains("checkout", runner.Commands[1].Arguments);
        Assert.Contains("v1.2.3", runner.Commands[1].Arguments);
        Assert.Equal(temp.NodePath, runner.Commands[1].WorkingDirectory);
    }

    [Fact]
    public async Task InstallAsync_DoesNotOverwriteExistingNodeDirectory()
    {
        using var temp = new TempComfyInstallRoot();
        Directory.CreateDirectory(temp.NodePath);
        var runner = new FakeProcessRunner();
        var service = CreateService(temp, runner);

        var result = await service.InstallAsync("https://github.com/acme/ComfyUI-TestNode");

        Assert.False(result.Success);
        Assert.Contains("已存在", result.ErrorMessage);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task InstallAsync_StopsBeforeInstallPyWhenPipFails()
    {
        using var temp = new TempComfyInstallRoot();
        var runner = new FakeProcessRunner();
        runner.AfterCommand = command =>
        {
            if (command.FileName == "git")
            {
                Directory.CreateDirectory(temp.NodePath);
                File.WriteAllText(Path.Combine(temp.NodePath, "requirements.txt"), "bad");
                File.WriteAllText(Path.Combine(temp.NodePath, "install.py"), "print('install')");
            }
        };
        runner.ExitCodesByArgument["-m pip install"] = 1;
        var service = CreateService(temp, runner);

        var result = await service.InstallAsync("https://github.com/acme/ComfyUI-TestNode");

        Assert.False(result.Success);
        Assert.Contains("requirements.txt", result.ErrorMessage);
        Assert.Equal(2, runner.Commands.Count);
        Assert.DoesNotContain(runner.Commands, c => c.Arguments == "install.py");
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/acme/node")]
    [InlineData("not a url")]
    public async Task InstallAsync_RejectsInvalidRepositoryUrl(string url)
    {
        using var temp = new TempComfyInstallRoot();
        var runner = new FakeProcessRunner();
        var service = CreateService(temp, runner);

        var result = await service.InstallAsync(url);

        Assert.False(result.Success);
        Assert.Empty(runner.Commands);
    }

    private static CustomNodeInstallerService CreateService(
        TempComfyInstallRoot temp,
        FakeProcessRunner runner,
        IProxyService? proxyService = null)
    {
        var pathService = new FakeComfyPathService(temp.ComfyUiPath, temp.ComfyRootPath);
        var pythonPathService = new FakePythonPathService(temp.PythonPath);

        return new CustomNodeInstallerService(
            pathService,
            pythonPathService,
            proxyService ?? new FakeProxyService(),
            runner,
            new FakeLogService());
    }

    private sealed class TempComfyInstallRoot : IDisposable
    {
        public TempComfyInstallRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "WpfDesktopCustomNodeTests", Guid.NewGuid().ToString("N"));
            ComfyRootPath = Root;
            ComfyUiPath = Path.Combine(Root, "ComfyUI");
            CustomNodesPath = Path.Combine(ComfyUiPath, "custom_nodes");
            PythonPath = Path.Combine(Root, "python_embeded", "python.exe");
            NodePath = Path.Combine(CustomNodesPath, "ComfyUI-TestNode");

            Directory.CreateDirectory(CustomNodesPath);
            Directory.CreateDirectory(Path.GetDirectoryName(PythonPath)!);
            File.WriteAllText(PythonPath, string.Empty);
        }

        public string Root { get; }
        public string ComfyRootPath { get; }
        public string ComfyUiPath { get; }
        public string CustomNodesPath { get; }
        public string PythonPath { get; }
        public string NodePath { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FakeProcessRunner : IExternalProcessRunner
    {
        public List<ExternalProcessCommand> Commands { get; } = new();
        public Dictionary<string, int> ExitCodesByArgument { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Action<ExternalProcessCommand>? AfterCommand { get; set; }

        public Task<ExternalProcessResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            AfterCommand?.Invoke(command);

            var exitCode = ExitCodesByArgument
                .Where(x => command.Arguments.Contains(x.Key, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Value)
                .FirstOrDefault();

            return Task.FromResult(new ExternalProcessResult(exitCode, "stdout", exitCode == 0 ? string.Empty : "stderr"));
        }
    }

    private sealed class FakeComfyPathService : IComfyPathService
    {
        public FakeComfyPathService(string comfyUiPath, string comfyRootPath)
        {
            ComfyUiPath = comfyUiPath;
            ComfyRootPath = comfyRootPath;
        }

        public string? ComfyUiPath { get; private set; }
        public string? ComfyRootPath { get; private set; }
        public bool IsValid { get; private set; } = true;
        public string? ErrorMessage { get; private set; }
        public void Refresh()
        {
        }
    }

    private sealed class FakePythonPathService : IPythonPathService
    {
        public FakePythonPathService(string pythonPath)
        {
            PythonPath = pythonPath;
        }

        public string? PythonPath { get; private set; }
        public bool IsValid => File.Exists(PythonPath);
        public void Resolve(string comfyRootPath)
        {
        }
    }

    private sealed class FakeProxyService : IProxyService
    {
        public bool IsEnabled => false;
        public bool IsGitHubMirrorEnabled => true;
        public bool IsPipMirrorEnabled => true;

        public string GetProxyServer() => string.Empty;
        public string ConvertGitHubUrl(string originalUrl) => originalUrl.Replace("https://github.com", "https://mirror.local/https://github.com", StringComparison.OrdinalIgnoreCase);
        public string GetPipMirrorArgs() => "-i https://pypi.example/simple --trusted-host pypi.example";

        public void ConfigureProcessProxy(ProcessStartInfo startInfo)
        {
            if (IsPipMirrorEnabled)
            {
                startInfo.Environment["PIP_INDEX_URL"] = "https://pypi.example/simple";
                startInfo.Environment["PIP_TRUSTED_HOST"] = "pypi.example";
            }
        }
    }

    private sealed class FakeLogService : ILogService
    {
        public event EventHandler<string>? LogReceived;
        public event EventHandler<LogEntry>? LogEntryReceived;
        public void Log(string message) => Log(message, GUILogLevel.Info);
        public void Log(string message, GUILogLevel level) => LogEntryReceived?.Invoke(this, new LogEntry { Message = message, Level = level });
        public void LogError(string message, Exception? exception = null) => Log(message, GUILogLevel.Error);
    }
}
