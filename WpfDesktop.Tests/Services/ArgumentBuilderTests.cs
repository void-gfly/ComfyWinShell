using WpfDesktop.Models;
using WpfDesktop.Models.Enums;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ArgumentBuilderTests
{
    private readonly ArgumentBuilder _builder = new();

    [Fact]
    public void BuildArguments_DefaultConfiguration_DoesNotEmitNewOptionalFlags()
    {
        var configuration = new ComfyConfiguration();

        var arguments = _builder.BuildArguments(configuration);

        Assert.DoesNotContain("--disable-auto-launch", arguments);
        Assert.DoesNotContain("--enable-assets", arguments);
        Assert.DoesNotContain("--disable-pinned-memory", arguments);
        Assert.DoesNotContain("--fp16-intermediates", arguments);
        Assert.DoesNotContain("--disable-async-offload", arguments);
        Assert.DoesNotContain("--enable-dynamic-vram", arguments);
        Assert.DoesNotContain("--disable-dynamic-vram", arguments);
        Assert.DoesNotContain("--cuda-malloc", arguments);
        Assert.DoesNotContain("--disable-cuda-malloc", arguments);
    }

    [Fact]
    public void BuildArguments_WhenAutoLaunchEnabled_EmitsAutoLaunch()
    {
        var configuration = new ComfyConfiguration
        {
            Launch = new LaunchConfiguration
            {
                AutoLaunch = true
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--auto-launch", arguments);
        Assert.DoesNotContain("--disable-auto-launch", arguments);
    }

    [Fact]
    public void BuildArguments_WhenDisableAutoLaunchEnabled_EmitsDisableAutoLaunch()
    {
        var configuration = new ComfyConfiguration
        {
            Launch = new LaunchConfiguration
            {
                DisableAutoLaunch = true
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--disable-auto-launch", arguments);
        Assert.DoesNotContain("--auto-launch", arguments);
    }

    [Fact]
    public void BuildArguments_WhenAutoLaunchAndDisableAutoLaunchEnabled_DisableAutoLaunchWins()
    {
        var configuration = new ComfyConfiguration
        {
            Launch = new LaunchConfiguration
            {
                AutoLaunch = true,
                DisableAutoLaunch = true
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--disable-auto-launch", arguments);
        Assert.DoesNotContain("--auto-launch", arguments);
    }

    [Fact]
    public void BuildArguments_WhenDynamicVramEnabled_EmitsEnableDynamicVram()
    {
        var configuration = new ComfyConfiguration
        {
            Memory = new MemoryConfiguration
            {
                DynamicVramMode = DynamicVramMode.Enable
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--enable-dynamic-vram", arguments);
        Assert.DoesNotContain("--disable-dynamic-vram", arguments);
    }

    [Fact]
    public void BuildArguments_WhenDynamicVramDisabled_EmitsDisableDynamicVram()
    {
        var configuration = new ComfyConfiguration
        {
            Memory = new MemoryConfiguration
            {
                DynamicVramMode = DynamicVramMode.Disable
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--disable-dynamic-vram", arguments);
        Assert.DoesNotContain("--enable-dynamic-vram", arguments);
    }

    [Fact]
    public void BuildArguments_WhenDisableAsyncOffloadEnabled_EmitsDisableAsyncOffload()
    {
        var configuration = new ComfyConfiguration
        {
            Memory = new MemoryConfiguration
            {
                DisableAsyncOffload = true
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--disable-async-offload", arguments);
        Assert.DoesNotContain("--async-offload", arguments);
    }

    [Fact]
    public void BuildArguments_WhenEnableAssetsEnabled_EmitsEnableAssets()
    {
        var configuration = new ComfyConfiguration
        {
            Miscellaneous = new MiscellaneousConfiguration
            {
                EnableAssets = true
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--enable-assets", arguments);
    }

    [Fact]
    public void BuildArguments_WhenDisablePinnedMemoryEnabled_EmitsDisablePinnedMemory()
    {
        var configuration = new ComfyConfiguration
        {
            Miscellaneous = new MiscellaneousConfiguration
            {
                DisablePinnedMemory = true
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--disable-pinned-memory", arguments);
    }

    [Fact]
    public void BuildArguments_WhenFp16IntermediatesEnabled_EmitsFp16Intermediates()
    {
        var configuration = new ComfyConfiguration
        {
            Miscellaneous = new MiscellaneousConfiguration
            {
                Fp16Intermediates = true
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains("--fp16-intermediates", arguments);
    }

    [Theory]
    [InlineData(CudaMallocMode.Enable, "--cuda-malloc", "--disable-cuda-malloc")]
    [InlineData(CudaMallocMode.Disable, "--disable-cuda-malloc", "--cuda-malloc")]
    public void BuildArguments_WhenCudaMallocModeConfigured_EmitsExpectedFlag(
        CudaMallocMode mode,
        string expectedFlag,
        string unexpectedFlag)
    {
        var configuration = new ComfyConfiguration
        {
            Miscellaneous = new MiscellaneousConfiguration
            {
                CudaMallocMode = mode
            }
        };

        var arguments = _builder.BuildArguments(configuration);

        Assert.Contains(expectedFlag, arguments);
        Assert.DoesNotContain(unexpectedFlag, arguments);
    }
}
