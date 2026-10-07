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

    [Theory]
    [InlineData("0,2", "A,C")]
    [InlineData("2,0", "C,A")]
    [InlineData("all", "A,B,C")]
    public void SelectVisibleGpus_MultiDeviceSelector_OverridesSingleDevice(string selector, string expected)
    {
        var gpus = new[] {
            new GpuInfoSnapshot { Name = "NVIDIA A" }, new GpuInfoSnapshot { Name = "NVIDIA B" },
            new GpuInfoSnapshot { Name = "NVIDIA C" }, new GpuInfoSnapshot { Name = "Intel" } };
        var result = GpuDisplaySelectionHelper.SelectVisibleGpus(gpus, true, 1, selector);
        Assert.Equal(expected, string.Join(",", result.Select(gpu => gpu.Name.Replace("NVIDIA ", ""))));
        Assert.Equal(gpus, GpuDisplaySelectionHelper.SelectVisibleGpus(gpus, false, 1, selector));
    }
}
