namespace WpfDesktop.Models;

/// <summary>
/// 表示一个 CUDA 设备选项，用于 ComboBox 显示
/// </summary>
public class CudaDeviceOption
{
    /// <summary>
    /// 设备 ID，null 表示"未选择"
    /// </summary>
    public int? DeviceId { get; init; }

    /// <summary>
    /// 显示名称，例如 "0: NVIDIA GeForce RTX 4080"
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 创建"无CUDA设备"选项
    /// </summary>
    public static CudaDeviceOption None => new()
    {
        DeviceId = null,
        DisplayName = "无CUDA设备"
    };

    /// <summary>
    /// 创建设备选项
    /// </summary>
    public static CudaDeviceOption Create(int deviceId, string gpuName)
    {
        return new CudaDeviceOption
        {
            DeviceId = deviceId,
            DisplayName = $"{deviceId}: {gpuName}"
        };
    }

    public override string ToString() => DisplayName;
}
