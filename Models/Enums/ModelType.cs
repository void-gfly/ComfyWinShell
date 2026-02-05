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
    Embedding,

    /// <summary>
    /// 文本编码器模型 (如 T5)
    /// </summary>
    TextEncoder,

    /// <summary>
    /// 其他类型
    /// </summary>
    Other,

    /// <summary>
    /// 音频编码器模型
    /// </summary>
    AudioEncoder,

    /// <summary>
    /// 音频分离模型
    /// </summary>
    AudioSeparation,

    /// <summary>
    /// 大语言模型 (LLM)
    /// </summary>
    LLM,

    /// <summary>
    /// 语音合成模型 (TTS)
    /// </summary>
    TTS,

    /// <summary>
    /// 视频插帧模型
    /// </summary>
    VideoInterpolation,

    /// <summary>
    /// SAM 分割模型
    /// </summary>
    Sam,

    /// <summary>
    /// DiT 扩散模型
    /// </summary>
    DiT,

    /// <summary>
    /// Wav2Vec 语音识别模型
    /// </summary>
    Wav2Vec,

    /// <summary>
    /// 模型补丁/ControlNet Union
    /// </summary>
    ModelPatch
}
