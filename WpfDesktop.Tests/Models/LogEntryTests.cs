using WpfDesktop.Models;
using Xunit;

namespace WpfDesktop.Tests.Models;

public sealed class LogEntryTests
{
    [Fact]
    public void FormattedMessage_UsesFullDateAndNoPrefix_ForNormalLogs()
    {
        var entry = new LogEntry
        {
            Message = "启动完成",
            Level = GUILogLevel.Info,
            Timestamp = new DateTime(2026, 5, 15, 13, 14, 15)
        };

        Assert.Equal("[2026-05-15 13:14:15] 启动完成", entry.FormattedMessage);
    }

    [Fact]
    public void FormattedMessage_UsesFullDateAndComfyPrefix_ForComfyOutput()
    {
        var entry = new LogEntry
        {
            Message = "模型加载完成",
            Level = GUILogLevel.ComfyRaw,
            Timestamp = new DateTime(2026, 5, 15, 13, 14, 15)
        };

        Assert.Equal("[2026-05-15 13:14:15] [Comfy] 模型加载完成", entry.FormattedMessage);
    }
}
