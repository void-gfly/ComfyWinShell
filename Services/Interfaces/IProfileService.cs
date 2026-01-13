using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface IProfileService
{
    Task<IReadOnlyList<Profile>> GetProfilesAsync();
    Task<Profile?> GetProfileAsync(string profileId);
    Task<Profile> CreateProfileAsync(string name, string? description = null);
    Task SaveProfileAsync(Profile profile);
    Task DeleteProfileAsync(string profileId);
    Task<bool> SetDefaultProfileAsync(string profileId);
    Task<Profile?> ImportProfileAsync(string filePath);
    Task ExportProfileAsync(Profile profile, string filePath);
}
