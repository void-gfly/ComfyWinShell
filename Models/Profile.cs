namespace WpfDesktop.Models;

public class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ComfyConfiguration Configuration { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastModified { get; set; }
    public bool IsDefault { get; set; }
}
