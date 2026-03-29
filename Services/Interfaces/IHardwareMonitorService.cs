namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 硬件监控服务接口。
/// </summary>
public interface IHardwareMonitorService : IDisposable
{
    /// <summary>
    /// 获取当前硬件快照
    /// </summary>
    HwInfoSnapshot GetSnapshot();

    /// <summary>
    /// 硬件监控是否可用
    /// </summary>
    bool IsAvailable { get; }
}
