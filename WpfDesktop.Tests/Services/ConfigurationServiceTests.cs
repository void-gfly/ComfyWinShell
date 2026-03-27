using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ConfigurationServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public ConfigurationServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "WpfDesktopTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenExtraModelBaseDirectoryMissing_ResetsToNull()
    {
        var missingPath = Path.Combine(_tempRoot, "missing-extra-models");
        await WriteProfileAsync(new Profile
        {
            Id = "default",
            Name = "default",
            Configuration = new ComfyConfiguration
            {
                Paths = new PathConfiguration
                {
                    ExtraModelBaseDirectory = missingPath
                }
            }
        });

        var service = CreateService();

        var configuration = await service.LoadConfigurationAsync("default");

        Assert.Null(configuration.Paths.ExtraModelBaseDirectory);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenExtraModelBaseDirectoryExists_KeepsValue()
    {
        var existingPath = Path.Combine(_tempRoot, "existing-extra-models");
        Directory.CreateDirectory(existingPath);

        await WriteProfileAsync(new Profile
        {
            Id = "default",
            Name = "default",
            Configuration = new ComfyConfiguration
            {
                Paths = new PathConfiguration
                {
                    ExtraModelBaseDirectory = existingPath
                }
            }
        });

        var service = CreateService();

        var configuration = await service.LoadConfigurationAsync("default");

        Assert.Equal(existingPath, configuration.Paths.ExtraModelBaseDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private ConfigurationService CreateService()
    {
        return new ConfigurationService(Options.Create(new AppSettings
        {
            DataRoot = _tempRoot
        }));
    }

    private async Task WriteProfileAsync(Profile profile)
    {
        var profilesDirectory = Path.Combine(_tempRoot, "profiles");
        Directory.CreateDirectory(profilesDirectory);

        var profilePath = Path.Combine(profilesDirectory, $"{profile.Id}.json");
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(profilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
