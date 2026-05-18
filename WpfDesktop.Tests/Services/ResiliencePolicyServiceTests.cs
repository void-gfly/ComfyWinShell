using System.Net.Http;
using Polly.Timeout;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ResiliencePolicyServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesTransientHttpExceptionsUntilSuccess()
    {
        var logService = new RecordingLogService();
        var service = new ResiliencePolicyService(
            logService,
            new ResiliencePolicyOptions
            {
                AttemptTimeout = TimeSpan.FromSeconds(1),
                OperationTimeout = TimeSpan.FromSeconds(2),
                RetryDelay = TimeSpan.Zero,
                MaxRetryAttempts = 3
            });

        var attempts = 0;

        var result = await service.ExecuteAsync("网络调用", _ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new HttpRequestException("temporary");
            }

            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
        Assert.Contains(logService.Entries, entry => entry.Level == GUILogLevel.Warning && entry.Message.Contains("准备指数回退重试", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetryNonTransientExceptions()
    {
        var logService = new RecordingLogService();
        var service = new ResiliencePolicyService(
            logService,
            new ResiliencePolicyOptions
            {
                AttemptTimeout = TimeSpan.FromSeconds(1),
                OperationTimeout = TimeSpan.FromSeconds(2),
                RetryDelay = TimeSpan.Zero,
                MaxRetryAttempts = 3
            });

        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("业务错误", _ =>
        {
            attempts++;
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal(1, attempts);
        Assert.DoesNotContain(logService.Entries, entry => entry.Message.Contains("准备指数回退重试", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsTimeoutRejectedExceptionWhenAttemptTimesOut()
    {
        var logService = new RecordingLogService();
        var service = new ResiliencePolicyService(
            logService,
            new ResiliencePolicyOptions
            {
                AttemptTimeout = TimeSpan.FromMilliseconds(20),
                OperationTimeout = TimeSpan.FromMilliseconds(100),
                RetryDelay = TimeSpan.Zero,
                MaxRetryAttempts = 1
            });

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => service.ExecuteAsync("超时操作", async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }));

        Assert.Contains(logService.Entries, entry => entry.Message.Contains("超时", StringComparison.Ordinal));
    }

    private sealed class RecordingLogService : ILogService
    {
        public event EventHandler<string>? LogReceived;

        public event EventHandler<LogEntry>? LogEntryReceived;

        public List<LogRecord> Entries { get; } = new();

        public void Log(string message)
        {
            Log(message, GUILogLevel.Info);
        }

        public void Log(string message, GUILogLevel level)
        {
            Entries.Add(new LogRecord(message, level));
            LogReceived?.Invoke(this, message);
            LogEntryReceived?.Invoke(this, new LogEntry { Message = message, Level = level, Timestamp = DateTime.Now });
        }

        public void LogError(string message, Exception? exception = null)
        {
            Entries.Add(new LogRecord(exception == null ? message : $"{message}: {exception.Message}", GUILogLevel.Error));
        }
    }

    private sealed record LogRecord(string Message, GUILogLevel Level);
}
