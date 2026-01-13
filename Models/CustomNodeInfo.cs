namespace WpfDesktop.Models;

/// <summary>
/// 自定义节点信息
/// </summary>
public class CustomNodeInfo
{
    /// <summary>
    /// 节点目录名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节点完整路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 是否为 Git 仓库
    /// </summary>
    public bool IsGitRepo { get; set; }

    /// <summary>
    /// Git 远程 URL（如果是 Git 仓库）
    /// </summary>
    public string? GitRemoteUrl { get; set; }
}
