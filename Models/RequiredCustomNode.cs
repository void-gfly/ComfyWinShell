namespace WpfDesktop.Models;

/// <summary>
/// 工作流所需的自定义节点信息
/// </summary>
public class RequiredCustomNode
{
    /// <summary>
    /// 节点类型（class_type）
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 在工作流中的使用次数
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// 是否为内置节点
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 节点是否存在（已安装）
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// 所属节点包名称（如果能识别）
    /// </summary>
    public string? PackageName { get; set; }

    /// <summary>
    /// 状态显示
    /// </summary>
    public string StatusDisplay => IsBuiltIn ? "内置" : (Exists ? "已安装" : "缺失");
}
