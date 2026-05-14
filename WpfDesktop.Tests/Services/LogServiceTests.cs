using WpfDesktop.Models;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class LogServiceTests
{
    [Fact]
    public void Log_ComfyRaw_EmitsPrefixedLegacyMessageAndTypedEntry()
    {
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
}
