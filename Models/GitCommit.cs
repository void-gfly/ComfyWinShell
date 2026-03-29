using System;

namespace WpfDesktop.Models;

/// <summary>
/// 表示一个 Git 提交记录。
/// </summary>
public class GitCommit
{
    /// <summary>
    /// 完整提交哈希值。
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// 用于界面展示的短提交哈希值。
    /// </summary>
    public string ShortHash { get; set; } = string.Empty;

    /// <summary>
    /// 提交说明消息。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 提交创建时间。
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 提交作者名称。
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 指示该提交是否为当前检出的提交。
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// 关联的标签名称；如果不存在标签则为空。
    /// </summary>
    public string? Tag { get; set; }
}
