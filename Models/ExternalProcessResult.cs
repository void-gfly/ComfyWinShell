namespace WpfDesktop.Models;

/// <summary>
/// 外部进程执行结果。
/// </summary>
public sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}
