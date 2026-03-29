namespace WpfDesktop.Models.Enums;

/// <summary>
/// 表示 UNet 使用的精度模式。
/// </summary>
public enum UnetPrecisionMode
{
    Default,
    Fp32,
    Fp64,
    Bf16,
    Fp16,
    Fp8E4m3fn,
    Fp8E5m2,
    Fp8E8m0fnu
}
