using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 读取 ComfyUI 可见 CUDA 设备顺序的服务。
/// </summary>
public interface ICudaDeviceDiscoveryService
{
    /// <summary>
    /// 获取按 ComfyUI / torch.cuda 顺序排列的 CUDA 设备列表。
    /// </summary>
    /// <returns>CUDA 设备列表。</returns>
    IReadOnlyList<CudaDeviceInfo> GetCudaDevices();
}
