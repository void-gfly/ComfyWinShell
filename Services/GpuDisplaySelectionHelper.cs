using WpfDesktop.Models;

namespace WpfDesktop.Services;

/// <summary>
/// 顶部栏 GPU 展示过滤辅助。
/// </summary>
public static class GpuDisplaySelectionHelper
{
    /// <summary>
    /// 根据全局设置与 CUDA 设备选择，返回顶部栏应显示的 GPU 列表。
    /// </summary>
    /// <param name="gpus">当前采集到的 GPU 列表。</param>
    /// <param name="showSelectedOnly">是否只显示选中的 GPU。</param>
    /// <param name="selectedCudaDevice">ComfyUI 配置中的 CUDA 设备编号。</param>
    /// <returns>应显示的 GPU 列表。</returns>
    public static IReadOnlyList<GpuInfoSnapshot> SelectVisibleGpus(
        IReadOnlyList<GpuInfoSnapshot> gpus,
        bool showSelectedOnly,
        int? selectedCudaDevice)
    {
        if (!showSelectedOnly)
        {
            return gpus.ToList();
        }

        if (!selectedCudaDevice.HasValue || selectedCudaDevice.Value < 0)
        {
            return gpus.ToList();
        }

        var selectedGpu = gpus
            .Where(IsNvidiaGpu)
            .ElementAtOrDefault(selectedCudaDevice.Value);

        if (selectedGpu == null)
        {
            return gpus.ToList();
        }

        return [selectedGpu];
    }

    private static bool IsNvidiaGpu(GpuInfoSnapshot gpu)
    {
        return gpu.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
    }
}
