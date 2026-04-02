using System.Text;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ConfigurationCompatibilityTests : IDisposable
{
    private readonly string _tempRoot;

    public ConfigurationCompatibilityTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "WpfDesktopTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenLegacyAutoLaunchExists_KeepsAutoLaunchEnabled()
    {
        await WriteProfileJsonAsync(
            """
            {
              "Id": "default",
              "Name": "default",
              "Configuration": {
                "Launch": {
                  "AutoLaunch": true
                }
              }
            }
            """);

        var service = CreateService();

        var configuration = await service.LoadConfigurationAsync("default");

        Assert.True(configuration.Launch.AutoLaunch);
        Assert.False(configuration.Launch.DisableAutoLaunch);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenLegacyDisableAssetsAutoscanExists_DoesNotEnableAssets()
    {
        await WriteProfileJsonAsync(
            """
            {
              "Id": "default",
              "Name": "default",
              "Configuration": {
                "Miscellaneous": {
                  "DisableAssetsAutoscan": true
                }
              }
            }
            """);

        var service = CreateService();

        var configuration = await service.LoadConfigurationAsync("default");

        Assert.False(configuration.Miscellaneous.EnableAssets);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenAutoLaunchAndDisableAutoLaunchConflict_DisableAutoLaunchWins()
    {
        await WriteProfileJsonAsync(
            """
            {
              "Id": "default",
              "Name": "default",
              "Configuration": {
                "Launch": {
                  "AutoLaunch": true,
                  "DisableAutoLaunch": true
                }
              }
            }
            """);

        var service = CreateService();

        var configuration = await service.LoadConfigurationAsync("default");

        Assert.True(configuration.Launch.DisableAutoLaunch);
        Assert.False(configuration.Launch.AutoLaunch);
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

    private async Task WriteProfileJsonAsync(string json)
    {
        var profilesDirectory = Path.Combine(_tempRoot, "profiles");
        Directory.CreateDirectory(profilesDirectory);

        var profilePath = Path.Combine(profilesDirectory, "default.json");
        await File.WriteAllTextAsync(profilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
