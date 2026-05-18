using WpfDesktop.Models;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

[Collection("FileLogWriter")]
public sealed class LogServiceTests
{
    [Fact]
    public void Log_ComfyRaw_EmitsPrefixedLegacyMessageAndTypedEntry()
    {
        FileLogWriter.ResetForTests();

        var service = new LogService();
        string? legacyMessage = null;
        LogEntry? typedEntry = null;

        service.LogReceived += (_, message) => legacyMessage = message;
        service.LogEntryReceived += (_, entry) => typedEntry = entry;

        service.Log("原始输出", GUILogLevel.ComfyRaw);

        Assert.NotNull(legacyMessage);
        Assert.Contains("[Comfy] 原始输出", legacyMessage);
        Assert.NotNull(typedEntry);
        Assert.Equal(GUILogLevel.ComfyRaw, typedEntry!.Level);
        Assert.Equal("原始输出", typedEntry.Message);
    }

    [Fact]
    public void LogError_WritesToFileWhenInitialized()
    {
        FileLogWriter.ResetForTests();

        var logRoot = Path.Combine(Path.GetTempPath(), $"WpfDesktop-Logs-{Guid.NewGuid():N}");
        var logDirectory = FileLogWriter.Initialize(logRoot, retentionDays: 14);

        Assert.NotNull(logDirectory);

        var service = new LogService();
        service.LogError("启动失败", new InvalidOperationException("boom"));

        var logFile = Path.Combine(logDirectory!, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        Assert.True(File.Exists(logFile));

        var content = File.ReadAllText(logFile);
        Assert.Contains("[ERROR]", content);
        Assert.Contains("启动失败", content);
        Assert.Contains("InvalidOperationException", content);
    }
}
