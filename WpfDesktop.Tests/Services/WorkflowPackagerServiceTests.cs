using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class WorkflowPackagerServiceTests
{
    [Fact]
    public async Task PackageWorkflowModelsOnlyAsync_CopiesModelsIntoModelsDirectory_AndKeepsExistingFiles()
    {
        using var tempRoot = new TempDirectory();
        var sourceModelPath = Path.Combine(tempRoot.Path, "source", "checkpoints", "model.safetensors");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceModelPath)!);
        await File.WriteAllTextAsync(sourceModelPath, "model-content");

        var targetRoot = Path.Combine(tempRoot.Path, "target");
        Directory.CreateDirectory(targetRoot);
        await File.WriteAllTextAsync(Path.Combine(targetRoot, "keep.txt"), "keep");

        var staleModelPath = Path.Combine(targetRoot, "models", "stale.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(staleModelPath)!);
        await File.WriteAllTextAsync(staleModelPath, "stale");

        var service = CreateService();
        var result = await service.PackageWorkflowModelsOnlyAsync(
            new WorkflowAnalysisResult
            {
                WorkflowName = "sample.json",
                WorkflowPath = Path.Combine(tempRoot.Path, "sample.json"),
                RequiredModels =
                [
                    new RequiredModel
                    {
                        ModelName = "model.safetensors",
                        ModelPath = "checkpoints/model.safetensors",
                        FullPath = sourceModelPath,
                        Exists = true,
                        SizeBytes = new FileInfo(sourceModelPath).Length
                    }
                ]
            },
            targetRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalModelsCopied);
        Assert.True(File.Exists(Path.Combine(targetRoot, "keep.txt")));
        Assert.True(File.Exists(staleModelPath));
        Assert.True(File.Exists(Path.Combine(targetRoot, "models", "checkpoints", "model.safetensors")));
        Assert.False(File.Exists(Path.Combine(targetRoot, "ComfyUI")));
    }

    [Fact]
    public async Task PackageBatchWorkflowModelsOnlyAsync_DeduplicatesSharedModels()
    {
        using var tempRoot = new TempDirectory();
        var sourceModelPath = Path.Combine(tempRoot.Path, "source", "loras", "shared.safetensors");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceModelPath)!);
        await File.WriteAllTextAsync(sourceModelPath, "shared-model");

        var targetRoot = Path.Combine(tempRoot.Path, "target");
        var service = CreateService();

        var analysisResults = new List<WorkflowAnalysisResult>
        {
            new()
            {
                WorkflowName = "one.json",
                WorkflowPath = Path.Combine(tempRoot.Path, "one.json"),
                RequiredModels =
                [
                    new RequiredModel
                    {
                        ModelName = "shared.safetensors",
                        ModelPath = "loras/shared.safetensors",
                        FullPath = sourceModelPath,
                        Exists = true,
                        SizeBytes = new FileInfo(sourceModelPath).Length
                    }
                ]
            },
            new()
            {
                WorkflowName = "two.json",
                WorkflowPath = Path.Combine(tempRoot.Path, "two.json"),
                RequiredModels =
                [
                    new RequiredModel
                    {
                        ModelName = "shared.safetensors",
                        ModelPath = "loras/shared.safetensors",
                        FullPath = sourceModelPath,
                        Exists = true,
                        SizeBytes = new FileInfo(sourceModelPath).Length
                    }
                ]
            }
        };

        var result = await service.PackageBatchWorkflowModelsOnlyAsync(analysisResults, targetRoot);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalModelsCopied);
        Assert.True(File.Exists(Path.Combine(targetRoot, "models", "loras", "shared.safetensors")));
    }

    private static WorkflowPackagerService CreateService()
    {
        return new WorkflowPackagerService(new DummyComfyPathService(), new RecordingLogService());
    }

    private sealed class DummyComfyPathService : IComfyPathService
    {
        public string? ComfyUiPath { get; private set; }
        public string? ComfyRootPath { get; private set; }
        public bool IsValid { get; private set; }
        public string? ErrorMessage { get; private set; }

        public void Refresh()
        {
        }
    }

    private sealed class RecordingLogService : ILogService
    {
        public event EventHandler<string>? LogReceived;
        public event EventHandler<LogEntry>? LogEntryReceived;

        public void Log(string message)
        {
            Log(message, GUILogLevel.Info);
        }

        public void Log(string message, GUILogLevel level)
        {
            LogEntryReceived?.Invoke(this, new LogEntry { Message = message, Level = level, Timestamp = DateTime.Now });
            LogReceived?.Invoke(this, message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            LogEntryReceived?.Invoke(this, new LogEntry { Message = message, Level = GUILogLevel.Error, Timestamp = DateTime.Now });
            LogReceived?.Invoke(this, message);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wpfdesktop-packager-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
