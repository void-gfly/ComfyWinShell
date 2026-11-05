using System.Diagnostics;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

internal readonly record struct HardwarePerformanceSnapshot(
    double? DiskReadMb, double? DiskWriteMb, double? MemoryPagesOutputPerSec);

/// <summary>
/// 独立采样可能耗时的 Windows 性能计数器，避免阻塞 CPU/GPU 传感器快照。
/// </summary>
internal sealed class HardwarePerformanceSampler : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Func<HardwarePerformanceSnapshot> _read;
    private readonly Action _release;
    private readonly ILogService? _logService;
    private HardwarePerformanceSnapshot _snapshot;
    private Task _samplingTask = Task.CompletedTask;
    private bool _sampling;
    private bool _failed;
    private bool _disposed;

    public HardwarePerformanceSampler(ILogService? logService)
    {
        var counters = new WindowsPerformanceCounters();
        _read = counters.Read;
        _release = counters.Dispose;
        _logService = logService;
    }

    internal HardwarePerformanceSampler(Func<HardwarePerformanceSnapshot> read, Action release, ILogService? logService)
    {
        _read = read;
        _release = release;
        _logService = logService;
    }

    internal Task SamplingTask
    {
        get { lock (_syncRoot) return _samplingTask; }
    }

    public HardwarePerformanceSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_sampling && !_failed)
            {
                _sampling = true;
                _samplingTask = Task.Run(Sample);
            }

            // 初次采样尚未完成时数值为 null，UI 显示 N/A，不伪造 0 或阻塞其他传感器。
            return _snapshot;
        }
    }

    private void Sample()
    {
        try
        {
            var snapshot = _read();
            lock (_syncRoot) _snapshot = snapshot;
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                _snapshot = default;
                _failed = true;
            }
            _logService?.LogError("Windows 磁盘/分页性能计数器采样失败", ex);
        }
        finally
        {
            bool release;
            lock (_syncRoot)
            {
                _sampling = false;
                release = _disposed;
            }
            if (release) _release();
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
            // 正在执行的同步系统调用无法取消，由采样任务完成后释放，不阻塞退出。
            if (_sampling) return;
        }
        _release();
    }

    private sealed class WindowsPerformanceCounters : IDisposable
    {
        private PerformanceCounter? _diskRead;
        private PerformanceCounter? _diskWrite;
        private PerformanceCounter? _pagesOutput;

        public HardwarePerformanceSnapshot Read()
        {
            _diskRead ??= new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
            _diskWrite ??= new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
            _pagesOutput ??= new PerformanceCounter("Memory", "Pages Output/sec", true);
            return new HardwarePerformanceSnapshot(
                _diskRead.NextValue() / (1024.0 * 1024.0),
                _diskWrite.NextValue() / (1024.0 * 1024.0),
                _pagesOutput.NextValue());
        }

        public void Dispose()
        {
            _diskRead?.Dispose();
            _diskWrite?.Dispose();
            _pagesOutput?.Dispose();
        }
    }
}
