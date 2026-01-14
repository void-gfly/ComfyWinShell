using WpfDesktop.Models.Enums;

namespace WpfDesktop.Models;

/// <summary>
/// 工作流所需的模型信息
/// </summary>
public class RequiredModel
{
    /// <summary>
    /// 模型名称（文件名）
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 模型相对路径
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// 模型完整路径（如果存在）
    /// </summary>
    public string? FullPath { get; set; }

    /// <summary>
    /// 模型类型
    /// </summary>
    public ModelType ModelType { get; set; } = ModelType.Unknown;

    /// <summary>
    /// 模型类型显示名称
    /// </summary>
    public string ModelTypeDisplay => ModelType switch
    {
        ModelType.Checkpoint => "Checkpoint",
        ModelType.Lora => "LoRA",
        ModelType.Vae => "VAE",
        ModelType.ControlNet => "ControlNet",
        ModelType.Clip => "CLIP",
        ModelType.ClipVision => "CLIP Vision",
        ModelType.Unet => "UNET",
        ModelType.StyleModel => "Style",
        ModelType.UpscaleModel => "Upscale",
        ModelType.Hypernetwork => "Hypernetwork",
        ModelType.Gligen => "GLIGEN",
        ModelType.Embedding => "Embedding",
        _ => "未知"
    };

    /// <summary>
    /// 加载器节点类型
    /// </summary>
    public string LoaderNodeType { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// 格式化的大小显示
    /// </summary>
    public string SizeDisplay
    {
        get
        {
            if (!Exists) return "-";
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
            if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// 模型文件是否存在
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// 状态显示
    /// </summary>
    public string StatusDisplay => Exists ? "已安装" : "缺失";
}
