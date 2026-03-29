namespace WpfDesktop.Models;

/// <summary>
/// 表示 ComfyUI 进程的运行状态快照。
/// </summary>
public class ProcessStatus
{
    /// <summary>
    /// 当前状态所属的版本标识符。
    /// </summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>
    /// 指示进程当前是否处于运行状态。
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 系统进程 ID；进程未启动时为空。
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// 进程启动时间。
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 进程已持续运行的时长。
    /// </summary>
    public TimeSpan? Uptime { get; set; }

    /// <summary>
    /// 最近一次错误信息。
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 采集到的标准输出和错误输出日志列表。
    /// </summary>
    public List<string> OutputLogs { get; set; } = new();

    /// <summary>
    /// 更细粒度的进程阶段状态。
    /// </summary>
    public ProcessState State { get; set; } = ProcessState.Idle;
}

/// <summary>
/// 表示进程生命周期中的阶段状态。
/// </summary>
public enum ProcessState
{
    Idle,
    Starting,
    Running,
    Recovering,
    Stopping,
    Stopped,
    Error
}
