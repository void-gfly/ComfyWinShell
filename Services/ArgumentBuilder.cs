using System.Globalization;
using Microsoft.Extensions.Logging;
using WpfDesktop.Models;
using WpfDesktop.Models.Enums;

namespace WpfDesktop.Services;

public class ArgumentBuilder
{
    public string BuildArguments(ComfyConfiguration configuration)
    {
        var args = new List<string>();

        AddNetworkArguments(args, configuration.Network);
        AddPathArguments(args, configuration.Paths);
        AddDeviceArguments(args, configuration.Device);
        AddMemoryArguments(args, configuration.Memory);
        AddPrecisionArguments(args, configuration.Precision);
        AddAttentionArguments(args, configuration.Attention);
        AddPreviewArguments(args, configuration.Preview);
        AddCacheArguments(args, configuration.Cache);
        AddManagerArguments(args, configuration.Manager);
        AddLaunchArguments(args, configuration.Launch);
        AddMiscArguments(args, configuration.Miscellaneous);

        return string.Join(" ", args);
    }

    private static void AddNetworkArguments(List<string> args, NetworkConfiguration network)
    {
        if (!string.IsNullOrWhiteSpace(network.Listen) && network.Listen != "127.0.0.1")
        {
            args.Add($"--listen {network.Listen}");
        }

        if (network.Port != 8188)
        {
            args.Add($"--port {network.Port}");
        }

        if (!string.IsNullOrWhiteSpace(network.TlsKeyFile))
        {
            args.Add($"--tls-keyfile {Quote(network.TlsKeyFile)}");
        }

        if (!string.IsNullOrWhiteSpace(network.TlsCertFile))
        {
            args.Add($"--tls-certfile {Quote(network.TlsCertFile)}");
        }

        if (network.MaxUploadSizeMb is > 0 and not 100)
        {
            args.Add($"--max-upload-size {network.MaxUploadSizeMb.ToString(CultureInfo.InvariantCulture)}");
        }

        if (network.CorsOrigin != null)
        {
            if (string.IsNullOrWhiteSpace(network.CorsOrigin) || network.CorsOrigin == "*")
            {
                args.Add("--enable-cors-header");
            }
            else
            {
                args.Add($"--enable-cors-header {network.CorsOrigin}");
            }
        }
    }

    private static void AddPathArguments(List<string> args, PathConfiguration paths)
    {
        if (!string.IsNullOrWhiteSpace(paths.BaseDirectory))
        {
            args.Add($"--base-directory {Quote(paths.BaseDirectory)}");
        }

        if (paths.ExtraModelPathsConfig.Count > 0)
        {
            foreach (var path in paths.ExtraModelPathsConfig)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    args.Add($"--extra-model-paths-config {Quote(path)}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(paths.OutputDirectory))
        {
            args.Add($"--output-directory {Quote(paths.OutputDirectory)}");
        }

        if (!string.IsNullOrWhiteSpace(paths.TempDirectory))
        {
            args.Add($"--temp-directory {Quote(paths.TempDirectory)}");
        }

        if (!string.IsNullOrWhiteSpace(paths.InputDirectory))
        {
            args.Add($"--input-directory {Quote(paths.InputDirectory)}");
        }

        if (!string.IsNullOrWhiteSpace(paths.UserDirectory))
        {
            args.Add($"--user-directory {Quote(paths.UserDirectory)}");
        }
    }

    private static void AddDeviceArguments(List<string> args, DeviceConfiguration device)
    {
        if (device.CudaDevice.HasValue)
        {
            args.Add($"--cuda-device {device.CudaDevice.Value}");
        }

        if (device.DefaultDevice.HasValue)
        {
            args.Add($"--default-device {device.DefaultDevice.Value}");
        }

        if (device.DirectMlDevice.HasValue)
        {
            args.Add($"--directml {device.DirectMlDevice.Value}");
        }

        if (!string.IsNullOrWhiteSpace(device.OneApiDeviceSelector))
        {
            args.Add($"--oneapi-device-selector {device.OneApiDeviceSelector}");
        }

        if (device.DisableIpexOptimize)
        {
            args.Add("--disable-ipex-optimize");
        }

        if (device.CpuVae)
        {
            args.Add("--cpu-vae");
        }
    }

    private static void AddMemoryArguments(List<string> args, MemoryConfiguration memory)
    {
        if (memory.VramMode != VramMode.Auto)
        {
            args.Add(memory.VramMode switch
            {
                VramMode.GpuOnly => "--gpu-only",
                VramMode.HighVram => "--highvram",
                VramMode.NormalVram => "--normalvram",
                VramMode.LowVram => "--lowvram",
                VramMode.NoVram => "--novram",
                VramMode.Cpu => "--cpu",
                _ => string.Empty
            });
        }

        if (memory.ReserveVramGb.HasValue)
        {
            args.Add($"--reserve-vram {memory.ReserveVramGb.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (memory.AsyncOffloadStreams.HasValue)
        {
            args.Add($"--async-offload {memory.AsyncOffloadStreams.Value}");
        }
        else if (memory.AsyncOffload)
        {
            args.Add("--async-offload");
        }

        if (!memory.SmartMemory)
        {
            args.Add("--disable-smart-memory");
        }
    }

    private static void AddPrecisionArguments(List<string> args, PrecisionConfiguration precision)
    {
        if (precision.ForcePrecision != ForcePrecisionMode.Default)
        {
            args.Add(precision.ForcePrecision switch
            {
                ForcePrecisionMode.Fp32 => "--force-fp32",
                ForcePrecisionMode.Fp16 => "--force-fp16",
                _ => string.Empty
            });
        }

        if (precision.UnetPrecision != UnetPrecisionMode.Default)
        {
            args.Add(precision.UnetPrecision switch
            {
                UnetPrecisionMode.Fp32 => "--fp32-unet",
                UnetPrecisionMode.Fp64 => "--fp64-unet",
                UnetPrecisionMode.Bf16 => "--bf16-unet",
                UnetPrecisionMode.Fp16 => "--fp16-unet",
                UnetPrecisionMode.Fp8E4m3fn => "--fp8_e4m3fn-unet",
                UnetPrecisionMode.Fp8E5m2 => "--fp8_e5m2-unet",
                UnetPrecisionMode.Fp8E8m0fnu => "--fp8_e8m0fnu-unet",
                _ => string.Empty
            });
        }

        if (precision.VaePrecision != VaePrecisionMode.Default)
        {
            args.Add(precision.VaePrecision switch
            {
                VaePrecisionMode.Fp16 => "--fp16-vae",
                VaePrecisionMode.Fp32 => "--fp32-vae",
                VaePrecisionMode.Bf16 => "--bf16-vae",
                _ => string.Empty
            });
        }

        if (precision.TextEncoderPrecision != TextEncoderPrecisionMode.Default)
        {
            args.Add(precision.TextEncoderPrecision switch
            {
                TextEncoderPrecisionMode.Fp8E4m3fn => "--fp8_e4m3fn-text-enc",
                TextEncoderPrecisionMode.Fp8E5m2 => "--fp8_e5m2-text-enc",
                TextEncoderPrecisionMode.Fp16 => "--fp16-text-enc",
                TextEncoderPrecisionMode.Fp32 => "--fp32-text-enc",
                TextEncoderPrecisionMode.Bf16 => "--bf16-text-enc",
                _ => string.Empty
            });
        }
    }

    private static void AddAttentionArguments(List<string> args, AttentionConfiguration attention)
    {
        if (attention.Mode != AttentionMode.Default)
        {
            args.Add(attention.Mode switch
            {
                AttentionMode.SplitCross => "--use-split-cross-attention",
                AttentionMode.QuadCross => "--use-quad-cross-attention",
                AttentionMode.Pytorch => "--use-pytorch-cross-attention",
                AttentionMode.Sage => "--use-sage-attention",
                AttentionMode.Flash => "--use-flash-attention",
                _ => string.Empty
            });
        }

        if (!attention.UseXFormers)
        {
            args.Add("--disable-xformers");
        }

        if (attention.UpcastMode == UpcastMode.ForceUpcast)
        {
            args.Add("--force-upcast-attention");
        }
        else if (attention.UpcastMode == UpcastMode.DontUpcast)
        {
            args.Add("--dont-upcast-attention");
        }
    }

    private static void AddPreviewArguments(List<string> args, PreviewConfiguration preview)
    {
        if (preview.Method != PreviewMethod.None)
        {
            var value = preview.Method switch
            {
                PreviewMethod.Auto => "auto",
                PreviewMethod.Latent2Rgb => "latent2rgb",
                PreviewMethod.Taesd => "taesd",
                _ => "none"
            };
            args.Add($"--preview-method {value}");
        }

        if (preview.PreviewSize != 512)
        {
            args.Add($"--preview-size {preview.PreviewSize}");
        }
    }

    private static void AddCacheArguments(List<string> args, CacheConfiguration cache)
    {
        switch (cache.Mode)
        {
            case CacheMode.Classic:
                args.Add("--cache-classic");
                break;
            case CacheMode.Lru:
                args.Add(cache.LruCount > 0 ? $"--cache-lru {cache.LruCount}" : "--cache-lru");
                break;
            case CacheMode.Ram:
                args.Add(cache.RamThresholdGb.HasValue
                    ? $"--cache-ram {cache.RamThresholdGb.Value.ToString(CultureInfo.InvariantCulture)}"
                    : "--cache-ram");
                break;
            case CacheMode.None:
                args.Add("--cache-none");
                break;
        }
    }

    private static void AddManagerArguments(List<string> args, ManagerConfiguration manager)
    {
        if (manager.EnableManager)
        {
            args.Add("--enable-manager");
        }

        if (manager.DisableManagerUi)
        {
            args.Add("--disable-manager-ui");
        }

        if (manager.EnableLegacyUi)
        {
            args.Add("--enable-manager-legacy-ui");
        }
    }

    private static void AddLaunchArguments(List<string> args, LaunchConfiguration launch)
    {
        if (launch.AutoLaunch)
        {
            args.Add("--auto-launch");
        }

        if (launch.DontPrintServer)
        {
            args.Add("--dont-print-server");
        }

        if (!string.IsNullOrWhiteSpace(launch.FrontEndVersion))
        {
            args.Add($"--front-end-version {launch.FrontEndVersion}");
        }

        if (!string.IsNullOrWhiteSpace(launch.FrontEndRoot))
        {
            args.Add($"--front-end-root {Quote(launch.FrontEndRoot)}");
        }
    }

    private static void AddMiscArguments(List<string> args, MiscellaneousConfiguration misc)
    {
        if (misc.ForceChannelsLast)
        {
            args.Add("--force-channels-last");
        }

        if (misc.SupportsFp8Compute)
        {
            args.Add("--supports-fp8-compute");
        }

        if (misc.ForceNonBlocking)
        {
            args.Add("--force-non-blocking");
        }

        if (!string.IsNullOrWhiteSpace(misc.DefaultHashingFunction) && misc.DefaultHashingFunction != "sha256")
        {
            args.Add($"--default-hashing-function {misc.DefaultHashingFunction}");
        }

        if (misc.Deterministic)
        {
            args.Add("--deterministic");
        }

        if (misc.FastOptions.Count > 0)
        {
            args.Add($"--fast {string.Join(" ", misc.FastOptions)}");
        }

        if (misc.MmapTorchFiles)
        {
            args.Add("--mmap-torch-files");
        }

        if (misc.DisableMmap)
        {
            args.Add("--disable-mmap");
        }

        if (misc.DisableMetadata)
        {
            args.Add("--disable-metadata");
        }

        if (misc.DisableAllCustomNodes)
        {
            args.Add("--disable-all-custom-nodes");
        }

        if (misc.WhitelistCustomNodes.Count > 0)
        {
            args.Add($"--whitelist-custom-nodes {string.Join(" ", misc.WhitelistCustomNodes)}");
        }

        if (misc.DisableApiNodes)
        {
            args.Add("--disable-api-nodes");
        }

        if (misc.MultiUser)
        {
            args.Add("--multi-user");
        }

        if (misc.Verbose != LogLevel.Information)
        {
            args.Add($"--verbose {ToVerboseValue(misc.Verbose)}");
        }

        if (misc.LogStdout)
        {
            args.Add("--log-stdout");
        }

        if (misc.EnableCompressResponseBody)
        {
            args.Add("--enable-compress-response-body");
        }

        if (!string.IsNullOrWhiteSpace(misc.ComfyApiBase))
        {
            args.Add($"--comfy-api-base {misc.ComfyApiBase}");
        }

        if (!string.IsNullOrWhiteSpace(misc.DatabaseUrl))
        {
            args.Add($"--database-url {misc.DatabaseUrl}");
        }

        if (misc.DisableAssetsAutoscan)
        {
            args.Add("--disable-assets-autoscan");
        }
    }

    private static string ToVerboseValue(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Warning => "WARNING",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            LogLevel.Trace => "DEBUG",
            _ => "INFO"
        };
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }
}
