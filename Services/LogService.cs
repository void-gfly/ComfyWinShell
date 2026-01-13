using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class LogService : ILogService
{
    public event EventHandler<string>? LogReceived;

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogReceived?.Invoke(this, $"[{timestamp}] {message}");
    }

    public void LogError(string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        if (exception != null)
        {
            LogReceived?.Invoke(this, $"[{timestamp}] [ERROR] {message}: {exception.Message}");
            LogReceived?.Invoke(this, $"[{timestamp}] [STACK] {exception.StackTrace}");
        }
        else
        {
            LogReceived?.Invoke(this, $"[{timestamp}] [ERROR] {message}");
        }
    }
}
