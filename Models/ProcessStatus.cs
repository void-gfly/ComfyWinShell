namespace WpfDesktop.Models;

public class ProcessStatus
{
    public string VersionId { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int? ProcessId { get; set; }
    public DateTime? StartTime { get; set; }
    public TimeSpan? Uptime { get; set; }
    public string? LastError { get; set; }
    public List<string> OutputLogs { get; set; } = new();
    public ProcessState State { get; set; } = ProcessState.Idle;
}

public enum ProcessState
{
    Idle,
    Starting,
    Running,
    Stopping,
    Stopped,
    Error
}
