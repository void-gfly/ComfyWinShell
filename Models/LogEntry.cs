namespace WpfDesktop.Models;

/// <summary>
/// GUI 日志级别
/// </summary>
public enum GUILogLevel
{
    /// <summary>
    /// 普通信息（白色）
    /// </summary>
    Info,

    /// <summary>
    /// 成功信息（绿色）
    /// </summary>
    Success,

    /// <summary>
    /// 警告信息（橙色/黄色）
    /// </summary>
    Warning,

    /// <summary>
    /// 错误信息（红色）
    /// </summary>
    Error
}

/// <summary>
/// 日志条目
/// </summary>
public sealed class LogEntry
{
    /// <summary>
    /// 日志内容
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 日志级别
    /// </summary>
    public GUILogLevel Level { get; set; } = GUILogLevel.Info;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 格式化的显示文本（包含时间戳）
    /// </summary>
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {Message}";
}
