using System.Text;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Models.Enums;
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

    [Fact]
    public async Task LoadConfigurationAsync_LegacyOptions_MigratesAndReportsChanges()
    {
        await WriteProfileJsonAsync("""
            { "Id": "default", "Configuration": {
                "Memory": { "VramMode": 3 },
                "Device": { "DisableIpexOptimize": true, "CudaDevice": 1 },
                "Miscellaneous": { "DisableApiNodes": true, "Verbose": 0, "FastOptions": ["autotune"] }
            } }
            """);
        var logs = new List<LogEntry>();
        var logger = new LogService();
        logger.LogEntryReceived += (_, entry) => logs.Add(entry);
        var service = new ConfigurationService(Options.Create(new AppSettings { DataRoot = _tempRoot }), logger);
        var config = await service.LoadConfigurationAsync("default");
        Assert.Equal(VramMode.Auto, config.Memory.VramMode);
        Assert.False(config.Device.DisableIpexOptimize);
        Assert.Equal(1, config.Device.CudaDevice);
        Assert.False(config.Miscellaneous.DisableApiNodes);
        Assert.True(config.Miscellaneous.Offline);
        Assert.Equal(ComfyLogLevel.Debug, config.Miscellaneous.Verbose);
        Assert.Equal(FastMode.Selected, config.Miscellaneous.FastMode);
        foreach (var keyword in new[] { "NormalVram", "IPEX", "离线模式", "日志级别", "快速优化" })
            Assert.Contains(logs, entry => entry.Level == GUILogLevel.Warning && entry.Message.Contains(keyword));
        var arguments = new ArgumentBuilder().BuildArguments(config);
        Assert.DoesNotContain("--normalvram", arguments);
        Assert.DoesNotContain("--disable-ipex-optimize", arguments);
        Assert.DoesNotContain("--disable-api-nodes", arguments);
        await service.SaveConfigurationAsync("default", config);
        var json = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "profiles", "default.json"), Encoding.UTF8);
        Assert.DoesNotContain("DisableIpexOptimize", json);
        Assert.DoesNotContain("DisableApiNodes", json);
        logs.Clear();
        await service.LoadConfigurationAsync("default");
        Assert.Empty(logs);
    }

    [Theory]
    [InlineData(1, ComfyLogLevel.Debug)]
    [InlineData(2, ComfyLogLevel.Information)]
    [InlineData(3, ComfyLogLevel.Warning)]
    [InlineData(4, ComfyLogLevel.Error)]
    [InlineData(5, ComfyLogLevel.Critical)]
    [InlineData(6, ComfyLogLevel.Information)]
    public async Task LoadConfigurationAsync_LegacyLogNumbers_PreserveMeaning(int value, ComfyLogLevel expected)
    {
        await WriteProfileJsonAsync($"{{\"Id\":\"default\",\"Configuration\":{{\"Miscellaneous\":{{\"Verbose\":{value}}}}}}}");
        Assert.Equal(expected, (await CreateService().LoadConfigurationAsync("default")).Miscellaneous.Verbose);
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
        }), new LogService());
    }

    private async Task WriteProfileJsonAsync(string json)
    {
        var profilesDirectory = Path.Combine(_tempRoot, "profiles");
        Directory.CreateDirectory(profilesDirectory);

        var profilePath = Path.Combine(profilesDirectory, "default.json");
        await File.WriteAllTextAsync(profilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
