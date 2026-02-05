using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _profilesDirectory;

    public ConfigurationService(IOptions<AppSettings> settings)
    {
        var dataRoot = PathHelper.ResolveDataRoot(settings.Value.DataRoot);
        _profilesDirectory = Path.Combine(dataRoot, "profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public async Task<ComfyConfiguration> LoadConfigurationAsync(string profileId)
    {
        var profile = await LoadProfileAsync(profileId);
        return profile?.Configuration ?? new ComfyConfiguration();
    }

    public async Task SaveConfigurationAsync(string profileId, ComfyConfiguration configuration)
    {
        var profile = await LoadProfileAsync(profileId) ?? new Profile { Id = profileId, Name = profileId };
        profile.Configuration = configuration;
        profile.LastModified = DateTime.Now;
        if (profile.CreatedAt == default)
        {
            profile.CreatedAt = DateTime.Now;
        }

        await SaveProfileAsync(profile);
    }

    public Task<bool> ValidateConfigurationAsync(ComfyConfiguration configuration)
    {
        if (configuration.Network.Port is < 1 or > 65535)
        {
            return Task.FromResult(false);
        }

        if (!string.IsNullOrWhiteSpace(configuration.Network.TlsKeyFile)
            && !File.Exists(configuration.Network.TlsKeyFile))
        {
            return Task.FromResult(false);
        }

        if (!string.IsNullOrWhiteSpace(configuration.Network.TlsCertFile)
            && !File.Exists(configuration.Network.TlsCertFile))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private async Task<Profile?> LoadProfileAsync(string profileId)
    {
        var filePath = GetProfilePath(profileId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<Profile>(stream, _serializerOptions);
    }

    private async Task SaveProfileAsync(Profile profile)
    {
        var filePath = GetProfilePath(profile.Id);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, _serializerOptions);
    }

    private string GetProfilePath(string profileId)
    {
        return Path.Combine(_profilesDirectory, $"{profileId}.json");
    }
}
