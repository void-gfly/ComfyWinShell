using System.Diagnostics;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class HardwareMonitorService : IHardwareMonitorService
{
    private HwInfo? _hwInfo;
    private bool _initialized;

    public bool IsAvailable => _hwInfo != null;

    public HwInfoSnapshot GetSnapshot()
    {
        EnsureInitialized();
        if (_hwInfo == null)
        {
            return new HwInfoSnapshot();
        }

        return _hwInfo.GetSnapshot();
    }

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

    public void Dispose()
    {
        _hwInfo?.Dispose();
    }
}
