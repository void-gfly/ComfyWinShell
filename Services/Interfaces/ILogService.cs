namespace WpfDesktop.Services.Interfaces;

public interface ILogService
{
    event EventHandler<string>? LogReceived;

    void Log(string message);
    void LogError(string message, Exception? exception = null);
}
