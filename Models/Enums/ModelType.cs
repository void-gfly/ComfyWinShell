namespace WpfDesktop.Models.Enums;

/// <summary>
/// ComfyUI 模型类型
/// </summary>
public enum ModelType
{
    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown,

    /// <summary>
    /// Checkpoint 主模型
    /// </summary>
    Checkpoint,

    /// <summary>
    /// LoRA 微调模型
    /// </summary>
    Lora,

    /// <summary>
    /// VAE 编码解码器
    /// </summary>
    Vae,

    /// <summary>
    /// ControlNet 控制模型
    /// </summary>
    ControlNet,

    /// <summary>
    /// CLIP 文本编码器
    /// </summary>
    Clip,

    /// <summary>
    /// CLIP Vision 图像编码器
    /// </summary>
    ClipVision,

    /// <summary>
    /// UNET 去噪模型
    /// </summary>
    Unet,

    /// <summary>
    /// Style 风格模型
    /// </summary>
    StyleModel,

    /// <summary>
    /// Upscale 超分辨率模型
    /// </summary>
    UpscaleModel,

    /// <summary>
    /// Hypernetwork 超网络
    /// </summary>
    Hypernetwork,

    /// <summary>
    /// GLIGEN 定位模型
    /// </summary>
    Gligen,

    /// <summary>
    /// Embedding 文本嵌入
    /// </summary>
    Embedding
}
