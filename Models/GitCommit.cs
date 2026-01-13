using System;

namespace WpfDesktop.Models;

public class GitCommit
{
    public string Hash { get; set; } = string.Empty;
    public string ShortHash { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Author { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string? Tag { get; set; }
}
