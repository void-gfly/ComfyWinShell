using System.Text.Json.Serialization;

namespace WpfDesktop.Models;

/// <summary>
/// 表示 ComfyUI 所见的 CUDA 设备信息。
/// </summary>
public sealed class CudaDeviceInfo
{
    /// <summary>
    /// CUDA 设备编号。
    /// </summary>
    [JsonPropertyName("deviceId")]
    public int DeviceId { get; init; }

    /// <summary>
    /// 设备名称。
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
