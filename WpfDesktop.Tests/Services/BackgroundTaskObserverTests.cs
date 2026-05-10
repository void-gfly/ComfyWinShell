using System.Collections.Concurrent;
using System.Net.Http;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class BackgroundTaskObserverTests
{
    [Fact]
    public void Observe_LogsWarningForRecoverableNetworkFault()
    {
        var logService = new RecordingLogService();
        var task = Task.FromException(new HttpRequestException("network down"));

        BackgroundTaskObserver.Observe(task, logService, "后台任务");

        Assert.True(logService.WaitForEntries());
        Assert.Single(logService.Entries);
        Assert.Equal(GUILogLevel.Warning, logService.Entries[0].Level);
        Assert.Contains("后台任务", logService.Entries[0].Message);
    }

    [Fact]
    public void Observe_LogsErrorForFatalFault()
    {
        var logService = new RecordingLogService();
        var task = Task.FromException(new InvalidOperationException("boom"));

        BackgroundTaskObserver.Observe(task, logService, "后台任务");

        Assert.True(logService.WaitForEntries());
        Assert.Single(logService.Errors);
        Assert.Equal("后台任务", logService.Errors[0].Message);
        Assert.IsType<InvalidOperationException>(logService.Errors[0].Exception);
    }

    private sealed class RecordingLogService : ILogService
    {
        private readonly ManualResetEventSlim _signal = new(false);

        public List<LogRecord> Entries { get; } = new();
        public List<ErrorRecord> Errors { get; } = new();

        public event EventHandler<string>? LogReceived;

        public event EventHandler<LogEntry>? LogEntryReceived;

        public void Log(string message)
        {
            Log(message, GUILogLevel.Info);
        }

        public void Log(string message, GUILogLevel level)
        {
            Entries.Add(new LogRecord(message, level));
            LogReceived?.Invoke(this, message);
            LogEntryReceived?.Invoke(this, new LogEntry { Message = message, Level = level, Timestamp = DateTime.Now });
            _signal.Set();
        }

        public void LogError(string message, Exception? exception = null)
        {
            Errors.Add(new ErrorRecord(message, exception));
            _signal.Set();
        }

        public bool WaitForEntries()
        {
            return _signal.Wait(TimeSpan.FromSeconds(1));
        }
    }

    private sealed record LogRecord(string Message, GUILogLevel Level);
    private sealed record ErrorRecord(string Message, Exception? Exception);
}
