namespace WpfDesktop.Models;

/// <summary>
/// 外部进程命令描述。
/// </summary>
public sealed class ExternalProcessCommand
{
    public string FileName { get; init; } = string.Empty;

    public string Arguments { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public Dictionary<string, string> Environment { get; } = new(StringComparer.OrdinalIgnoreCase);
}
