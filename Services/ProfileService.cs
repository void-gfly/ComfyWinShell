using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class ProfileService : IProfileService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _profilesDirectory;

    public ProfileService(IOptions<AppSettings> settings)
    {
        var dataRoot = PathHelper.ResolveDataRoot(settings.Value.DataRoot);
        _profilesDirectory = Path.Combine(dataRoot, "profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public async Task<IReadOnlyList<Profile>> GetProfilesAsync()
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            return Array.Empty<Profile>();
        }

        var profiles = new List<Profile>();
        foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var profile = await JsonSerializer.DeserializeAsync<Profile>(stream, _serializerOptions);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    public async Task<Profile?> GetProfileAsync(string profileId)
    {
        var filePath = GetProfilePath(profileId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<Profile>(stream, _serializerOptions);
    }

    public async Task<Profile> CreateProfileAsync(string name, string? description = null)
    {
        var profile = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = string.IsNullOrWhiteSpace(name) ? "新配置" : name,
            Description = description,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now
        };

        await SaveProfileAsync(profile);
        return profile;
    }

    public async Task<bool> SetDefaultProfileAsync(string profileId)
    {
        var profiles = (await GetProfilesAsync()).ToList();
        var found = false;
        foreach (var profile in profiles)
        {
            var isDefault = profile.Id == profileId;
            if (profile.IsDefault != isDefault)
            {
                profile.IsDefault = isDefault;
                profile.LastModified = DateTime.Now;
            }

            if (isDefault)
            {
                found = true;
            }
        }

        foreach (var profile in profiles)
        {
            await SaveProfileAsync(profile);
        }

        return found;
    }

    public async Task<Profile?> ImportProfileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        var profile = await JsonSerializer.DeserializeAsync<Profile>(stream, _serializerOptions);
        if (profile == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.Id) || File.Exists(GetProfilePath(profile.Id)))
        {
            profile.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = "导入配置";
        }

        profile.CreatedAt = DateTime.Now;
        profile.LastModified = DateTime.Now;

        await SaveProfileAsync(profile);
        return profile;
    }

    public async Task ExportProfileAsync(Profile profile, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _profilesDirectory);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, _serializerOptions);
    }

    public async Task SaveProfileAsync(Profile profile)
    {
        profile.LastModified = DateTime.Now;
        if (profile.CreatedAt == default)
        {
            profile.CreatedAt = DateTime.Now;
        }

        var filePath = GetProfilePath(profile.Id);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, _serializerOptions);
    }

    public Task DeleteProfileAsync(string profileId)
    {
        var filePath = GetProfilePath(profileId);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private string GetProfilePath(string profileId)
    {
        return Path.Combine(_profilesDirectory, $"{profileId}.json");
    }
}
