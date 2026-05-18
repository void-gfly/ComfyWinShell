using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

[Collection("FileLogWriter")]
public sealed class FileLogWriterTests
{
    [Fact]
    public void Initialize_CleansUpExpiredLogFiles()
    {
        FileLogWriter.ResetForTests();

        var logRoot = Path.Combine(Path.GetTempPath(), $"WpfDesktop-Logs-{Guid.NewGuid():N}");
        var logsDirectory = Path.Combine(logRoot, "logs");
        Directory.CreateDirectory(logsDirectory);

        var oldLogFile = Path.Combine(logsDirectory, DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd") + ".log");
        File.WriteAllText(oldLogFile, "old");

        var logDirectory = FileLogWriter.Initialize(logRoot, retentionDays: 14);

        Assert.Equal(logsDirectory, logDirectory);

        FileLogWriter.Log("测试", "清理验证");

        Assert.False(File.Exists(oldLogFile));

        var currentLogFile = Path.Combine(logsDirectory, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        Assert.True(File.Exists(currentLogFile));
        Assert.Contains("清理验证", File.ReadAllText(currentLogFile));
    }
}