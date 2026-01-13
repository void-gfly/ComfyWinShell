namespace WpfDesktop.Models;

public class ComfyVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public VersionType Type { get; set; }
    public string? InstallPath { get; set; }
    public string? PythonPath { get; set; }
    public string? GitUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitCommit { get; set; }
    public string? DownloadUrl { get; set; }
    public long? Size { get; set; }
    public string? Sha256 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastUsed { get; set; }
    public bool IsActive { get; set; }
    public bool IsCorrupted { get; set; }
}

public enum VersionType
{
    Git,
    Local,
    Portable,
    Custom
}
