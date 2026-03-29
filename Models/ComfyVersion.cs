namespace WpfDesktop.Models;

/// <summary>
/// 表示一个 ComfyUI 版本实例及其安装信息。
/// </summary>
public class ComfyVersion
{
    /// <summary>
    /// 当前版本记录的唯一标识符。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 版本显示名称，通常用于界面列表展示。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 版本号或版本描述字符串。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 当前版本的来源类型，例如 Git、本地目录或便携包。
    /// </summary>
    public VersionType Type { get; set; }

    /// <summary>
    /// ComfyUI 实际安装目录路径。
    /// </summary>
    public string? InstallPath { get; set; }

    /// <summary>
    /// 该版本使用的 Python 可执行文件或环境路径。
    /// </summary>
    public string? PythonPath { get; set; }

    /// <summary>
    /// 如果该版本来自 Git 仓库，则记录远程仓库地址。
    /// </summary>
    public string? GitUrl { get; set; }

    /// <summary>
    /// Git 分支名称，用于标识当前版本跟踪的分支。
    /// </summary>
    public string? GitBranch { get; set; }

    /// <summary>
    /// 当前版本对应的 Git 提交哈希。
    /// </summary>
    public string? GitCommit { get; set; }

    /// <summary>
    /// 如果该版本可下载，则记录其下载地址。
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// 安装包或目录对应的大小，单位为字节。
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// 下载文件或安装包的 SHA-256 校验值。
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// 该版本记录的创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 最近一次使用该版本的时间。
    /// </summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// 指示该版本当前是否被设为活动版本。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 指示该版本是否被检测为损坏或不可用。
    /// </summary>
    public bool IsCorrupted { get; set; }
}

/// <summary>
/// 表示 ComfyUI 版本的来源或安装类型。
/// </summary>
public enum VersionType
{
    Git,
    Local,
    Portable,
    Custom
}
