using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class GpuDisplaySelectionHelperTests
{
    [Fact]
    public void SelectVisibleGpus_WhenFilteringDisabled_ReturnsAllGpus()
    {
        var gpus = CreateGpus();

        var result = GpuDisplaySelectionHelper.SelectVisibleGpus(gpus, showSelectedOnly: false, selectedCudaDevice: 1);

        Assert.Equal(3, result.Count);
        Assert.Equal("AMD Radeon", result[0].Name);
        Assert.Equal("NVIDIA RTX 4070", result[1].Name);
        Assert.Equal("NVIDIA RTX 4090", result[2].Name);
    }

    [Fact]
    public void SelectVisibleGpus_WhenFilteringEnabledAndSelectedCudaDeviceExists_ReturnsOnlySelectedNvidiaGpu()
    {
        var gpus = CreateGpus();

        var result = GpuDisplaySelectionHelper.SelectVisibleGpus(gpus, showSelectedOnly: true, selectedCudaDevice: 1);

        Assert.Single(result);
        Assert.Equal("NVIDIA RTX 4090", result[0].Name);
    }

    [Fact]
    public void SelectVisibleGpus_WhenFilteringEnabledAndSelectedCudaDeviceMissing_ReturnsAllGpus()
    {
        var gpus = CreateGpus();

        var result = GpuDisplaySelectionHelper.SelectVisibleGpus(gpus, showSelectedOnly: true, selectedCudaDevice: 5);

        Assert.Equal(3, result.Count);
    }

    private static IReadOnlyList<GpuInfoSnapshot> CreateGpus()
    {
        return new List<GpuInfoSnapshot>
        {
            new() { Name = "AMD Radeon" },
            new() { Name = "NVIDIA RTX 4070" },
            new() { Name = "NVIDIA RTX 4090" }
        };
    }
}
