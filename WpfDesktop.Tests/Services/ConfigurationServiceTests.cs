using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Models.Enums;
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

    [Fact]
    public async Task SaveAndLoadAsync_PreservesCudaDeviceSelection()
    {
        var service = CreateService();
        var configuration = new ComfyConfiguration
        {
            Device = new DeviceConfiguration
            {
                CudaDevice = 1
            }
        };

        await service.SaveConfigurationAsync("default", configuration);

        var reloaded = await service.LoadConfigurationAsync("default");

        Assert.Equal(1, reloaded.Device.CudaDevice);
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesAllowRemoteCustomNodeInstall()
    {
        var service = CreateService();
        var configuration = new ComfyConfiguration
        {
            Network = new NetworkConfiguration
            {
                AllowRemoteCustomNodeInstall = true
            }
        };

        await service.SaveConfigurationAsync("default", configuration);

        var reloaded = await service.LoadConfigurationAsync("default");

        Assert.True(reloaded.Network.AllowRemoteCustomNodeInstall);
    }

    [Fact]
    public void NewConfiguration_DefaultsAllowRemoteCustomNodeInstallToFalse()
    {
        var configuration = new ComfyConfiguration();

        Assert.False(configuration.Network.AllowRemoteCustomNodeInstall);
    }

    [Fact]
    public async Task SaveAndLoadAsync_NewSettings_PreservesAllValuesAndExplicitOffMode()
    {
        var config = new ComfyConfiguration();
        config.Paths.ModelsDirectory = _tempRoot;
        config.Device.CudaDeviceSelector = "0,1";
        config.Memory.VramHeadroomGb = 1.25;
        config.Memory.FastDiskMode = FeatureMode.Disable;
        config.Memory.DisableNvmlPressure = true;
        config.Attention.Mode = AttentionMode.ComfyKitchen;
        config.Cache.Mode = CacheMode.Ram;
        config.Cache.RamThresholdGb = 3;
        config.Cache.InactiveRamThresholdGb = 8;
        config.Miscellaneous.Verbose = ComfyLogLevel.Detail;
        config.Miscellaneous.TritonMode = FeatureMode.Enable;
        config.Miscellaneous.Offline = true;
        config.Miscellaneous.DisablePartnerNodes = true;
        config.Miscellaneous.EnableAssetHashing = true;
        config.Miscellaneous.DisableComfyCompiler = true;
        config.Miscellaneous.DisableCudaGraphs = true;
        config.Miscellaneous.AssertGraphBreaks = true;
        config.Miscellaneous.DebugHang = true;
        config.Miscellaneous.FeatureFlags.Add("labels=a,b");
        config.Miscellaneous.LogFiles.Add(new LogFileConfiguration { Level = ComfyLogLevel.Warning, Path = "日志 文件.log" });
        config.Miscellaneous.FastOptions.Add("autotune");
        config.Miscellaneous.FastMode = FastMode.Off;
        var service = CreateService();
        await service.SaveConfigurationAsync("default", config);
        var loaded = await service.LoadConfigurationAsync("default");
        Assert.Equal(JsonSerializer.Serialize(config), JsonSerializer.Serialize(loaded));
        Assert.DoesNotContain("--fast ", new ArgumentBuilder().BuildArguments(loaded));
    }

    [Fact]
    public async Task SaveConfigurationAsync_InvalidSettings_DoNotWriteProfile()
    {
        var config = new ComfyConfiguration();
        config.Device.CudaDeviceSelector = "0,,1";
        var service = CreateService();
        Assert.False(await service.ValidateConfigurationAsync(config));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveConfigurationAsync("default", config));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "profiles", "default.json")));
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
