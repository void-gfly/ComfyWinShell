using System.Diagnostics;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 硬件监控服务实现。
/// </summary>
public class HardwareMonitorService : IHardwareMonitorService
{
    private HwInfo? _hwInfo;
    private readonly ICudaDeviceDiscoveryService _cudaDeviceDiscoveryService;
    private readonly ILogService _logService;
    private bool _initialized;

    public HardwareMonitorService(ICudaDeviceDiscoveryService cudaDeviceDiscoveryService, ILogService logService)
    {
        _cudaDeviceDiscoveryService = cudaDeviceDiscoveryService;
        _logService = logService;
    }

    public bool IsAvailable => _hwInfo != null;

    /// <summary>
    /// 获取当前硬件状态快照。
    /// </summary>
    /// <returns>当前硬件信息快照。</returns>
    public HwInfoSnapshot GetSnapshot()
    {
        EnsureInitialized();
        if (_hwInfo == null)
        {
            return new HwInfoSnapshot();
        }

        var snapshot = _hwInfo.GetSnapshot();
        var cudaDevices = _cudaDeviceDiscoveryService.GetCudaDevices();
        if (snapshot.Gpus.Count == 0 || cudaDevices.Count == 0)
        {
            return snapshot;
        }

        snapshot.Gpus = CudaDeviceOrderHelper.OrderGpusByCudaDevices(snapshot.Gpus, cudaDevices);
        return snapshot;
    }

    /// <summary>
    /// 确保底层硬件监控对象只初始化一次。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _hwInfo = new HwInfo(_logService);
        }
        catch (Exception ex)
        {
            _logService.LogError("初始化硬件监控库失败", ex);
            Debug.WriteLine($"硬件监控初始化失败: {ex.Message}");
            _hwInfo = null;
        }
    }

    /// <summary>
    /// 释放硬件监控资源。
    /// </summary>
    public void Dispose()
    {
        _hwInfo?.Dispose();
    }
}
