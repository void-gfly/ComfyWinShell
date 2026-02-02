namespace WpfDesktop.Models;

/// <summary>
/// 工作流打包结果
/// </summary>
public class WorkflowPackageResult
{
    /// <summary>
    /// 打包是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误信息（如果打包失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 打包目标路径
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// 打包开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 打包结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 打包耗时
    /// </summary>
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

    /// <summary>
    /// 复制的文件总数
    /// </summary>
    public int TotalFilesCopied { get; set; }

    /// <summary>
    /// 复制的模型总数
    /// </summary>
    public int TotalModelsCopied { get; set; }

    /// <summary>
    /// 打包后的总大小（字节）
    /// </summary>
    public long TotalSizeBytes { get; set; }
}
