using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface ILogService
{
    /// <summary>
    /// 日志接收事件（旧版本，保持向后兼容）
    /// </summary>
    event EventHandler<string>? LogReceived;

    /// <summary>
    /// 带级别的日志接收事件
    /// </summary>
    event EventHandler<LogEntry>? LogEntryReceived;

    /// <summary>
    /// 记录普通日志（白色）
    /// </summary>
    void Log(string message);

    /// <summary>
    /// 记录带级别的日志
    /// </summary>
    void Log(string message, GUILogLevel level);

    /// <summary>
    /// 记录错误日志（红色）
    /// </summary>
    void LogError(string message, Exception? exception = null);
}
