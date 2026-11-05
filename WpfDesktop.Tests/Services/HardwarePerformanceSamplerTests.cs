using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class HardwarePerformanceSamplerTests
{
    [Fact]
    public async Task SlowCounterDoesNotBlockSnapshotsOrStartDuplicateReads()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var reads = 0;
        var expected = new HardwarePerformanceSnapshot(12, 34, 56);
        using var sampler = new HardwarePerformanceSampler(() =>
        {
            Interlocked.Increment(ref reads);
            started.TrySetResult();
            release.Wait();
            return expected;
        }, () => { }, null);

        var first = sampler.GetSnapshot();
        var pending = sampler.SamplingTask;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var duringRead = await Task.Run(sampler.GetSnapshot).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(default, first);
            Assert.Equal(default, duringRead);
            Assert.Equal(1, Volatile.Read(ref reads));
            Assert.Same(pending, sampler.SamplingTask);
        }
        finally
        {
            release.Set();
            await pending;
        }

        Assert.Equal(expected, sampler.GetSnapshot());
        await sampler.SamplingTask;
    }

    [Fact]
    public async Task DisposeDoesNotWaitForBlockedCounterAndReleasesAfterRead()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var disposals = 0;
        var sampler = new HardwarePerformanceSampler(() =>
        {
            started.SetResult();
            release.Wait();
            return new HardwarePerformanceSnapshot(1, 2, 3);
        }, () => Interlocked.Increment(ref disposals), null);
        sampler.GetSnapshot();
        var pending = sampler.SamplingTask;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Run(sampler.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, Volatile.Read(ref disposals));
            Assert.Throws<ObjectDisposedException>(() => sampler.GetSnapshot());
        }
        finally
        {
            release.Set();
            await pending;
            sampler.Dispose();
        }

        Assert.Equal(1, Volatile.Read(ref disposals));
    }

    [Fact]
    public async Task FailedCounterLogsOnceWithoutRetryingOrReturningStaleValues()
    {
        var reads = 0;
        var log = new RecordingLog();
        using var sampler = new HardwarePerformanceSampler(() =>
        {
            if (Interlocked.Increment(ref reads) == 1) return new HardwarePerformanceSnapshot(1, 2, 3);
            throw new InvalidOperationException("counter unavailable");
        }, () => { }, log);

        sampler.GetSnapshot();
        await sampler.SamplingTask;
        Assert.Equal(new HardwarePerformanceSnapshot(1, 2, 3), sampler.GetSnapshot());
        await sampler.SamplingTask;
        Assert.Equal(default, sampler.GetSnapshot());
        Assert.Equal(default, sampler.GetSnapshot());
        Assert.Equal(2, Volatile.Read(ref reads));
        Assert.Contains("counter unavailable", Assert.Single(log.Errors));
    }

    [Fact]
    public void DisposeBeforeFirstSampleDoesNotReadAndOnlyReleasesOnce()
    {
        var reads = 0;
        var disposals = 0;
        var sampler = new HardwarePerformanceSampler(() =>
        {
            reads++;
            return default;
        }, () => disposals++, null);
        sampler.Dispose();
        sampler.Dispose();
        Assert.Throws<ObjectDisposedException>(() => sampler.GetSnapshot());
        Assert.Equal(0, reads);
        Assert.Equal(1, disposals);
    }

    private sealed class RecordingLog : ILogService
    {
        public event EventHandler<string>? LogReceived { add { } remove { } }
        public event EventHandler<LogEntry>? LogEntryReceived { add { } remove { } }
        public List<string> Errors { get; } = new();
        public void Log(string message) { }
        public void Log(string message, GUILogLevel level) { }
        public void LogError(string message, Exception? exception = null) => Errors.Add($"{message}: {exception?.Message}");
    }
}
