using System.Globalization;
using System.IO;
using WpfDesktop.Models;
using WpfDesktop.Models.Enums;

namespace WpfDesktop.Services;

public static class ComfyConfigurationValidator
{
    public static IReadOnlyList<string> FastOptions { get; } =
        ["fp16_accumulation", "fp8_matrix_mult", "cublas_ops", "autotune"];

    public static IReadOnlyList<ComfyLogLevel> LogLevels { get; } =
        [ComfyLogLevel.Debug, ComfyLogLevel.Detail, ComfyLogLevel.Information,
         ComfyLogLevel.Warning, ComfyLogLevel.Error, ComfyLogLevel.Critical];

    public static bool TryParseCudaSelector(string selector, out int[] ids)
    {
        ids = [];
        if (selector.Trim() == "all") return true;
        var values = selector.Split(',', StringSplitOptions.TrimEntries);
        var parsed = new List<int>();
        foreach (var value in values)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || parsed.Contains(id)) return false;
            parsed.Add(id);
        }
        ids = parsed.ToArray();
        return ids.Length > 0;
    }

    public static IReadOnlyList<string> Validate(ComfyConfiguration configuration)
    {
        var errors = new List<string>();
        var network = configuration.Network;
        var device = configuration.Device;
        var memory = configuration.Memory;
        var cache = configuration.Cache;
        var misc = configuration.Miscellaneous;
        if (!string.IsNullOrWhiteSpace(configuration.Paths.ModelsDirectory)
            && !Directory.Exists(configuration.Paths.ModelsDirectory))
            errors.Add($"模型目录不存在：{configuration.Paths.ModelsDirectory}");
        if (network.Port is < 1 or > 65535) errors.Add("监听端口必须为 1–65535。");
        if (!double.IsFinite(network.MaxUploadSizeMb) || network.MaxUploadSizeMb <= 0)
            errors.Add("最大上传大小必须为大于零的有限数值。");
        foreach (var path in new[] { network.TlsKeyFile, network.TlsCertFile })
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path)) errors.Add($"TLS 文件不存在：{path}");
        if (string.IsNullOrWhiteSpace(network.TlsKeyFile) != string.IsNullOrWhiteSpace(network.TlsCertFile))
            errors.Add("TLS 密钥与证书必须同时设置。");
        if (!string.IsNullOrWhiteSpace(device.CudaDeviceSelector)
            && !TryParseCudaSelector(device.CudaDeviceSelector, out _))
            errors.Add("CUDA 设备必须为非负整数列表（如 0,1）或 all，不允许重复编号。");
        if (device.CudaDevice < 0 || device.DefaultDevice < 0 || device.DirectMlDevice < -1)
            errors.Add("CUDA/默认设备编号不能为负数；DirectML 可使用 -1 自动选择。");
        if (memory.VramMode == VramMode.NormalVram || device.DisableIpexOptimize || misc.DisableApiNodes)
            errors.Add("配置包含已移除或弃用的设置，请重新加载配置完成迁移。");
        ValidateEnum(memory.VramMode, "显存模式", errors);
        ValidateEnum(memory.DynamicVramMode, "动态显存模式", errors);
        ValidateEnum(memory.FastDiskMode, "快速磁盘模式", errors);
        ValidateEnum(configuration.Attention.Mode, "注意力模式", errors);
        ValidateEnum(configuration.Attention.UpcastMode, "Upcast 模式", errors);
        ValidateEnum(configuration.Precision.ForcePrecision, "强制精度", errors);
        ValidateEnum(configuration.Precision.UnetPrecision, "UNet 精度", errors);
        ValidateEnum(configuration.Precision.VaePrecision, "VAE 精度", errors);
        ValidateEnum(configuration.Precision.TextEncoderPrecision, "TextEncoder 精度", errors);
        ValidateEnum(configuration.Preview.Method, "预览方法", errors);
        ValidateEnum(cache.Mode, "缓存模式", errors);
        ValidateEnum(misc.FastMode, "快速优化模式", errors);
        ValidateEnum(misc.TritonMode, "Triton 模式", errors);
        ValidateEnum(misc.CudaMallocMode, "CUDA Malloc 模式", errors);
        ValidateNonnegative(memory.ReserveVramGb, "预留显存", errors);
        ValidateNonnegative(memory.VramHeadroomGb, "显存余量", errors);
        if (!memory.DisableAsyncOffload && memory.AsyncOffloadStreams <= 0)
            errors.Add("异步卸载流数量必须大于零。");
        if (cache.Mode == CacheMode.Lru && cache.LruCount < 0) errors.Add("LRU 数量不能为负数。");
        if (cache.Mode == CacheMode.Ram)
        {
            ValidateNonnegative(cache.RamThresholdGb, "活动缓存阈值", errors);
            ValidateNonnegative(cache.InactiveRamThresholdGb, "非活动缓存阈值", errors);
            if (cache.InactiveRamThresholdGb.HasValue && !cache.RamThresholdGb.HasValue)
                errors.Add("设置非活动缓存阈值时，必须同时设置活动缓存阈值。");
        }
        if (configuration.Manager.DisableManagerUi && configuration.Manager.EnableLegacyUi)
            errors.Add("禁用管理器 UI 与旧版管理器 UI 不能同时启用。");
        if (configuration.Precision.ForcePrecision == ForcePrecisionMode.Fp16
            && configuration.Precision.UnetPrecision is not (UnetPrecisionMode.Default or UnetPrecisionMode.Fp16))
            errors.Add("强制 FP16 与所选 UNet 精度冲突。");
        if (configuration.Preview.PreviewSize <= 0) errors.Add("预览尺寸必须大于零。");
        if (!LogLevels.Contains(misc.Verbose)) errors.Add("请选择 ComfyUI 支持的日志级别。");
        foreach (var file in misc.LogFiles)
            if (!LogLevels.Contains(file.Level) || string.IsNullOrWhiteSpace(file.Path)
                || file.Path.StartsWith('-') || file.Path.IndexOfAny(['\r', '\n', '\0']) >= 0)
                errors.Add("每个日志文件必须填写有效路径并选择支持的日志级别。");
        if (misc.FastMode == FastMode.Selected
            && (misc.FastOptions.Count == 0 || misc.FastOptions.Any(option => !FastOptions.Contains(option))))
            errors.Add("指定快速优化时必须选择官方选项：fp16_accumulation、fp8_matrix_mult、cublas_ops、autotune。");
        foreach (var flag in misc.FeatureFlags)
            if (string.IsNullOrWhiteSpace(flag) || flag.StartsWith('-') || flag.Split('=', 2)[0].Trim().Length == 0
                || flag.IndexOfAny(['\r', '\n', '\0']) >= 0)
                errors.Add("Feature flag 必须为 KEY 或 KEY=VALUE，每行一项。");
        if (!new[] { "md5", "sha1", "sha256", "sha512" }.Contains(misc.DefaultHashingFunction))
            errors.Add("哈希函数必须为 md5、sha1、sha256 或 sha512。");
        return errors;
    }

    public static void ThrowIfInvalid(ComfyConfiguration configuration)
    {
        var errors = Validate(configuration);
        if (errors.Count > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(configuration));
    }

    private static void ValidateEnum<T>(T value, string label, List<string> errors) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) errors.Add($"{label}的值无效。");
    }

    private static void ValidateNonnegative(double? value, string label, List<string> errors)
    {
        if (value.HasValue && (!double.IsFinite(value.Value) || value.Value < 0))
            errors.Add($"{label}必须为非负有限数值。");
    }
}
