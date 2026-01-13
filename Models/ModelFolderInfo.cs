namespace WpfDesktop.Models;

/// <summary>
/// 模型文件夹信息
/// </summary>
public class ModelFolderInfo
{
    /// <summary>
    /// 文件夹名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 文件夹完整路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 磁盘占用大小（字节）
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// 是否正在计算大小
    /// </summary>
    public bool IsCalculating { get; set; } = true;

    /// <summary>
    /// 格式化的大小显示（GB）
    /// </summary>
    public string SizeDisplay => IsCalculating 
        ? "计算中..." 
        : $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";

    /// <summary>
    /// 文件数量
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// 文件数量显示
    /// </summary>
    public string FileCountDisplay => IsCalculating ? "-" : FileCount.ToString();

    /// <summary>
    /// 目录功能介绍
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
