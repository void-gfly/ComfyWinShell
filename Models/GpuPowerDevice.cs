namespace WpfDesktop.Models;

/// <summary>
/// nvidia-smi 返回的设备身份及功耗上限，单位为 W。
/// </summary>
public sealed record GpuPowerDevice(
    int Index,
    string Uuid,
    string Name,
    decimal? PowerLimit,
    decimal? MinPowerLimit,
    decimal? MaxPowerLimit,
    decimal? DefaultPowerLimit)
{
    public string DisplayName => $"GPU {Index}: {Name}";

    public bool CanSetPowerLimit => PowerLimit.HasValue && MinPowerLimit.HasValue && MaxPowerLimit.HasValue;
}
