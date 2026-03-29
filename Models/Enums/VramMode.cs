namespace WpfDesktop.Models.Enums;

/// <summary>
/// 表示显存占用与卸载的策略模式。
/// </summary>
public enum VramMode
{
    Auto,
    GpuOnly,
    HighVram,
    NormalVram,
    LowVram,
    NoVram,
    Cpu
}
