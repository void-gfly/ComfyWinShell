namespace WpfDesktop.Models;

/// <summary>
/// 表示一份可保存和切换的 ComfyUI 配置档案。
/// </summary>
public class Profile
{
    /// <summary>
    /// 配置档案的唯一标识符。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 配置档案名称，用于界面展示和用户识别。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 配置档案的补充说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 档案对应的 ComfyUI 详细配置对象。
    /// </summary>
    public ComfyConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// 该档案的创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 该档案最近一次修改时间。
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// 指示该档案是否为默认档案。
    /// </summary>
    public bool IsDefault { get; set; }
}
