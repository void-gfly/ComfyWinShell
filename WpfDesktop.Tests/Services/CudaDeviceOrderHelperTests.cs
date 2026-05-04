using WpfDesktop.Models;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class CudaDeviceOrderHelperTests
{
    [Fact]
    public void OrderGpusByCudaDevices_ReordersByTorchDeviceNames()
    {
        var gpus = new List<GpuInfoSnapshot>
        {
            new GpuInfoSnapshot { Name = "NVIDIA GeForce RTX 3080" },
            new GpuInfoSnapshot { Name = "NVIDIA GeForce RTX 4070" },
            new GpuInfoSnapshot { Name = "NVIDIA GeForce RTX 4090" }
        };

        var cudaDevices = new List<CudaDeviceInfo>
        {
            new CudaDeviceInfo { DeviceId = 0, Name = "NVIDIA GeForce RTX 4090" },
            new CudaDeviceInfo { DeviceId = 1, Name = "NVIDIA GeForce RTX 4070" },
            new CudaDeviceInfo { DeviceId = 2, Name = "NVIDIA GeForce RTX 3080" }
        };

        var ordered = CudaDeviceOrderHelper.OrderGpusByCudaDevices(gpus, cudaDevices);

        Assert.Equal("NVIDIA GeForce RTX 4090", ordered[0].Name);
        Assert.Equal("NVIDIA GeForce RTX 4070", ordered[1].Name);
        Assert.Equal("NVIDIA GeForce RTX 3080", ordered[2].Name);
    }

    [Fact]
    public void OrderGpusByCudaDevices_PreservesDuplicateNamesInSequence()
    {
        var gpus = new List<GpuInfoSnapshot>
        {
            new GpuInfoSnapshot { Name = "NVIDIA GeForce RTX 4090" },
            new GpuInfoSnapshot { Name = "NVIDIA GeForce RTX 4090" },
            new GpuInfoSnapshot { Name = "NVIDIA GeForce RTX 4070" }
        };

        var cudaDevices = new List<CudaDeviceInfo>
        {
            new CudaDeviceInfo { DeviceId = 0, Name = "NVIDIA GeForce RTX 4090" },
            new CudaDeviceInfo { DeviceId = 1, Name = "NVIDIA GeForce RTX 4090" },
            new CudaDeviceInfo { DeviceId = 2, Name = "NVIDIA GeForce RTX 4070" }
        };

        var ordered = CudaDeviceOrderHelper.OrderGpusByCudaDevices(gpus, cudaDevices);

        Assert.Equal("NVIDIA GeForce RTX 4090", ordered[0].Name);
        Assert.Equal("NVIDIA GeForce RTX 4090", ordered[1].Name);
        Assert.Equal("NVIDIA GeForce RTX 4070", ordered[2].Name);
    }
}
