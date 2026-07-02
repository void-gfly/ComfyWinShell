using System.Text;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ComfyManagerSettingsServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public ComfyManagerSettingsServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "WpfDesktopTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ApplyRemoteCustomNodeInstallAsync_WhenEnabled_CreatesManagerConfigWithInstallFlags()
    {
        var comfyUiPath = CreateComfyUiPath();
        var service = new ComfyManagerSettingsService();

        await service.ApplyRemoteCustomNodeInstallAsync(comfyUiPath, userDirectory: null, enabled: true);

        var configPath = Path.Combine(comfyUiPath, "user", "default", "ComfyUI-Manager", "config.ini");
        var config = await File.ReadAllTextAsync(configPath, Encoding.UTF8);
        Assert.Contains("[default]", config);
        Assert.Contains("allow_git_url_install = true", config);
        Assert.Contains("allow_pip_install = true", config);
    }

    [Fact]
    public async Task ApplyRemoteCustomNodeInstallAsync_WhenDisabled_PreservesOtherConfigAndWritesFalse()
    {
        var comfyUiPath = CreateComfyUiPath();
        var configDirectory = Path.Combine(comfyUiPath, "user", "default", "ComfyUI-Manager");
        Directory.CreateDirectory(configDirectory);
        var configPath = Path.Combine(configDirectory, "config.ini");
        await File.WriteAllTextAsync(
            configPath,
            """
            [default]
            security_level = normal
            allow_git_url_install = true

            [network]
            bypass_ssl = false
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var service = new ComfyManagerSettingsService();

        await service.ApplyRemoteCustomNodeInstallAsync(comfyUiPath, userDirectory: null, enabled: false);

        var config = await File.ReadAllTextAsync(configPath, Encoding.UTF8);
        Assert.Contains("security_level = normal", config);
        Assert.Contains("[network]", config);
        Assert.Contains("bypass_ssl = false", config);
        Assert.Contains("allow_git_url_install = false", config);
        Assert.Contains("allow_pip_install = false", config);
    }

    [Fact]
    public async Task ApplyRemoteCustomNodeInstallAsync_WhenDisabledAndConfigMissing_DoesNotCreateConfig()
    {
        var comfyUiPath = CreateComfyUiPath();
        var service = new ComfyManagerSettingsService();

        await service.ApplyRemoteCustomNodeInstallAsync(comfyUiPath, userDirectory: null, enabled: false);

        Assert.False(File.Exists(Path.Combine(comfyUiPath, "user", "default", "ComfyUI-Manager", "config.ini")));
    }

    [Fact]
    public async Task ApplyRemoteCustomNodeInstallAsync_WhenUserDirectoryConfigured_UsesThatUserDirectory()
    {
        var comfyUiPath = CreateComfyUiPath();
        var userDirectory = Path.Combine(_tempRoot, "custom-user");
        var service = new ComfyManagerSettingsService();

        await service.ApplyRemoteCustomNodeInstallAsync(comfyUiPath, userDirectory, enabled: true);

        var expectedConfigPath = Path.Combine(userDirectory, "default", "ComfyUI-Manager", "config.ini");
        Assert.True(File.Exists(expectedConfigPath));
        Assert.False(File.Exists(Path.Combine(comfyUiPath, "user", "default", "ComfyUI-Manager", "config.ini")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateComfyUiPath()
    {
        var comfyUiPath = Path.Combine(_tempRoot, "ComfyUI");
        Directory.CreateDirectory(comfyUiPath);
        return comfyUiPath;
    }
}
