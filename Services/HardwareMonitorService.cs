using System.Diagnostics;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 硬件监控服务实现。
/// </summary>
public class HardwareMonitorService : IHardwareMonitorService
{
    private HwInfo? _hwInfo;
    private bool _initialized;

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

        return _hwInfo.GetSnapshot();
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
            _hwInfo = new HwInfo();
        }
        catch (Exception ex)
        {
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
