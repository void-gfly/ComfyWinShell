using WpfDesktop.Models;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public SettingsServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "WpfDesktopTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesShowSelectedGpuOnly()
    {
        var service = CreateService();
        var settings = new AppSettings
        {
            DataRoot = _tempRoot,
            ShowSelectedGpuOnly = true
        };

        await service.SaveAsync(settings);

        var reloaded = await CreateService().LoadAsync();

        Assert.True(reloaded.ShowSelectedGpuOnly);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private SettingsService CreateService()
    {
        return new SettingsService(new AppSettings
        {
            DataRoot = _tempRoot
        });
    }
}
