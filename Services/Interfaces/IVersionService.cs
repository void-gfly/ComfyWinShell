using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface IVersionService
{
    Task<IReadOnlyList<ComfyVersion>> GetAllVersionsAsync();
    Task<ComfyVersion?> GetVersionByIdAsync(string versionId);
    Task<ComfyVersion?> GetActiveVersionAsync();
    Task SaveVersionAsync(ComfyVersion version);
    Task DeleteVersionAsync(string versionId);
    Task<bool> SetActiveVersionAsync(string versionId);
    Task<bool> ValidateVersionAsync(ComfyVersion version);
    Task<ComfyVersion?> CreateLocalVersionAsync(string path, string? name = null);
}
