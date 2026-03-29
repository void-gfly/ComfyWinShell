namespace WpfDesktop.Models.Enums;

/// <summary>
/// 表示 ComfyUI 可选的注意力实现模式。
/// </summary>
public enum AttentionMode
{
    Default,
    SplitCross,
    QuadCross,
    Pytorch,
    Sage,
    Flash
}
