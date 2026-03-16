using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfDesktop.Models;

/// <summary>
/// 工作流文件信息
/// </summary>
public partial class WorkflowInfo : ObservableObject
{
    /// <summary>
    /// 工作流文件名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工作流完整路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

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
            if (SizeBytes < 1024)
                return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024)
                return $"{SizeBytes / 1024.0:F1} KB";
            return $"{SizeBytes / (1024.0 * 1024.0):F2} MB";
        }
    }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// 是否选中（用于批量打包）
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
}
