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

    [Fact]
    public void BuildArguments_NewDefaultConfiguration_UsesOfficialDefaults()
    {
        Assert.Equal(string.Empty, _builder.BuildArguments(new ComfyConfiguration()));
    }

    [Theory]
    [InlineData(FeatureMode.Default, "")]
    [InlineData(FeatureMode.Enable, "--fast-disk --enable-triton-backend")]
    [InlineData(FeatureMode.Disable, "--disable-fast-disk --disable-triton-backend")]
    public void BuildArguments_FeatureModes_EmitOnlySelectedFlags(FeatureMode mode, string expected)
    {
        var config = new ComfyConfiguration();
        config.Memory.FastDiskMode = mode;
        config.Miscellaneous.TritonMode = mode;
        Assert.Equal(expected, _builder.BuildArguments(config));
    }

    [Fact]
    public void BuildArguments_NewRuntimeOptions_EmitSupportedFlags()
    {
        var config = new ComfyConfiguration();
        config.Memory.VramHeadroomGb = 1.5;
        config.Memory.DisableNvmlPressure = true;
        config.Attention.Mode = AttentionMode.ComfyKitchen;
        config.Cache.Mode = CacheMode.HighRam;
        config.Miscellaneous.Offline = true;
        config.Miscellaneous.DisablePartnerNodes = true;
        config.Miscellaneous.EnableAssetHashing = true;
        config.Miscellaneous.DisableCudaGraphs = true;
        config.Miscellaneous.DisableComfyCompiler = true;
        config.Miscellaneous.AssertGraphBreaks = true;
        config.Miscellaneous.DebugHang = true;
        foreach (var flag in new[] { "--vram-headroom 1.5", "--disable-nvml-pressure", "--use-ck-attention",
            "--high-ram", "--offline", "--disable-partner-nodes", "--enable-asset-hashing",
            "--disable-cuda-graphs", "--disable-comfy-compiler", "--assert-graph-breaks", "--debug-hang" })
            Assert.Contains(flag, _builder.BuildArguments(config));
    }

    [Theory]
    [InlineData(null, null, "--cache-ram")]
    [InlineData(3.5, null, "--cache-ram 3.5")]
    [InlineData(3.5, 8.25, "--cache-ram 3.5 8.25")]
    public void BuildArguments_RamCache_EmitsZeroToTwoValues(double? active, double? inactive, string expected)
    {
        var config = new ComfyConfiguration();
        config.Cache.Mode = CacheMode.Ram;
        config.Cache.RamThresholdGb = active;
        config.Cache.InactiveRamThresholdGb = inactive;
        Assert.Equal(expected, _builder.BuildArguments(config));
    }

    [Fact]
    public void BuildArguments_LruZero_StillIncludesRequiredInteger()
    {
        var config = new ComfyConfiguration();
        config.Cache.Mode = CacheMode.Lru;
        Assert.Equal("--cache-lru 0", _builder.BuildArguments(config));
    }

    [Fact]
    public void BuildArguments_DisabledAsyncOffload_DoesNotEmitEnabledStreams()
    {
        var config = new ComfyConfiguration();
        config.Memory.AsyncOffload = true;
        config.Memory.AsyncOffloadStreams = 3;
        config.Memory.DisableAsyncOffload = true;
        Assert.Equal("--disable-async-offload", _builder.BuildArguments(config));
    }

    [Theory]
    [InlineData("0,1", "--cuda-device 0,1")]
    [InlineData("all", "--cuda-device all")]
    [InlineData(" all\t", "--cuda-device all")]
    [InlineData("0,\t1", "--cuda-device 0,1")]
    [InlineData("0, 1", "--cuda-device 0,1")]
    public void BuildArguments_CudaSelector_OverridesSavedSingleDevice(string selector, string expected)
    {
        var config = new ComfyConfiguration();
        config.Device.CudaDevice = 5;
        config.Device.CudaDeviceSelector = selector;
        Assert.False(config.Device.IsSingleCudaSelectionEnabled);
        Assert.Equal(expected, _builder.BuildArguments(config));
        config.Device.CudaDeviceSelector = null;
        Assert.True(config.Device.IsSingleCudaSelectionEnabled);
        Assert.Equal("--cuda-device 5", _builder.BuildArguments(config));
    }

    [Theory]
    [InlineData(FastMode.Off, "")]
    [InlineData(FastMode.All, "--fast")]
    [InlineData(FastMode.Selected, "--fast fp16_accumulation autotune")]
    public void BuildArguments_FastMode_ControlsOptimizationScope(FastMode mode, string expected)
    {
        var config = new ComfyConfiguration();
        config.Miscellaneous.FastMode = mode;
        config.Miscellaneous.FastOptions.Add("fp16_accumulation");
        config.Miscellaneous.FastOptions.Add("autotune");
        Assert.Equal(expected, _builder.BuildArguments(config));
    }

    [Fact]
    public void BuildArguments_RepeatedFlagsAndLogFiles_PreserveValuesAndEscapeWindowsPaths()
    {
        var config = new ComfyConfiguration();
        config.Miscellaneous.FeatureFlags.Add("show_signin_button=false");
        config.Miscellaneous.FeatureFlags.Add("labels=a,b");
        config.Miscellaneous.FeatureFlags.Add("label=hello \"world\"");
        config.Miscellaneous.Verbose = ComfyLogLevel.Detail;
        config.Miscellaneous.LogFiles.Add(new LogFileConfiguration { Level = ComfyLogLevel.Debug, Path = @"C:\日志 目录\debug.log" });
        config.Miscellaneous.LogFiles.Add(new LogFileConfiguration { Level = ComfyLogLevel.Warning, Path = @"C:\日志\warn.log" });
        config.Paths.OutputDirectory = @"C:\输出 目录\";
        config.Miscellaneous.WhitelistCustomNodes.Add("Node Folder");
        var args = _builder.BuildArguments(config);
        Assert.Contains("--feature-flag show_signin_button=false --feature-flag labels=a,b", args);
        Assert.Contains("--feature-flag \"label=hello \\\"world\\\"\"", args);
        Assert.Contains("--verbose DEBUG \"C:\\日志 目录\\debug.log\"", args);
        Assert.Contains("--verbose WARNING C:\\日志\\warn.log", args);
        Assert.Contains("--verbose DETAIL", args);
        Assert.Contains("--output-directory \"C:\\输出 目录\\\\\"", args);
        Assert.Contains("--whitelist-custom-nodes \"Node Folder\"", args);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0,,1")]
    [InlineData("0,0")]
    [InlineData("0,x")]
    public void BuildArguments_InvalidCudaSelector_IsRejected(string selector)
    {
        var config = new ComfyConfiguration();
        config.Device.CudaDeviceSelector = selector;
        Assert.Throws<ArgumentException>(() => _builder.BuildArguments(config));
    }

    [Fact]
    public void BuildArguments_InvalidConfiguration_ReportsAllRelevantErrors()
    {
        var config = new ComfyConfiguration();
        config.Cache.Mode = CacheMode.Ram;
        config.Cache.InactiveRamThresholdGb = 3;
        config.Memory.VramHeadroomGb = double.NaN;
        config.Miscellaneous.FastMode = FastMode.Selected;
        config.Miscellaneous.FastOptions.Add("unsupported");
        config.Miscellaneous.LogFiles.Add(new LogFileConfiguration());
        config.Manager.DisableManagerUi = config.Manager.EnableLegacyUi = true;
        var errors = ComfyConfigurationValidator.Validate(config);
        Assert.Contains(errors, error => error.Contains("必须同时设置活动"));
        Assert.Contains(errors, error => error.Contains("非负有限"));
        Assert.Contains(errors, error => error.Contains("官方选项"));
        Assert.Contains(errors, error => error.Contains("日志文件"));
        Assert.Contains(errors, error => error.Contains("不能同时启用"));
        Assert.Throws<ArgumentException>(() => _builder.BuildArguments(config));
    }
}
