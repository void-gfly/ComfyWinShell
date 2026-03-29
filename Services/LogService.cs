using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// UI 日志分发服务。
/// </summary>
public class LogService : ILogService
{
    /// <summary>
    /// 旧版本事件，保持向后兼容
    /// </summary>
    public event EventHandler<string>? LogReceived;

    /// <summary>
    /// 带级别的日志事件
    /// </summary>
    public event EventHandler<LogEntry>? LogEntryReceived;

    /// <summary>
    /// 记录普通级别日志。
    /// </summary>
    /// <param name="message">日志内容。</param>
    public void Log(string message)
    {
        Log(message, GUILogLevel.Info);
    }

    /// <summary>
    /// 按指定级别记录日志。
    /// </summary>
    /// <param name="message">日志内容。</param>
    /// <param name="level">日志级别。</param>
    public void Log(string message, GUILogLevel level)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = new LogEntry
        {
            Message = message,
            Level = level,
            Timestamp = DateTime.Now
        };

        // 触发新事件
        LogEntryReceived?.Invoke(this, entry);

        // 触发旧事件（向后兼容）
        LogReceived?.Invoke(this, $"[{timestamp}] {message}");
    }

    /// <summary>
    /// 记录错误日志，并在存在异常时附带异常信息。
    /// </summary>
    /// <param name="message">错误描述。</param>
    /// <param name="exception">关联异常对象。</param>
    public void LogError(string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        
        if (exception != null)
        {
            var errorMessage = $"{message}: {exception.Message}";
            var entry = new LogEntry
            {
                Message = errorMessage,
                Level = GUILogLevel.Error,
                Timestamp = DateTime.Now
            };
            LogEntryReceived?.Invoke(this, entry);
            LogReceived?.Invoke(this, $"[{timestamp}] [ERROR] {errorMessage}");

            // 堆栈信息
            var stackEntry = new LogEntry
            {
                Message = $"[STACK] {exception.StackTrace}",
                Level = GUILogLevel.Error,
                Timestamp = DateTime.Now
            };
            LogEntryReceived?.Invoke(this, stackEntry);
            LogReceived?.Invoke(this, $"[{timestamp}] [STACK] {exception.StackTrace}");
        }
        else
        {
            var entry = new LogEntry
            {
                Message = message,
                Level = GUILogLevel.Error,
                Timestamp = DateTime.Now
            };
            LogEntryReceived?.Invoke(this, entry);
            LogReceived?.Invoke(this, $"[{timestamp}] [ERROR] {message}");
        }
    }
}
