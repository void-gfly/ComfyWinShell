using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Models.Enums;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// ComfyUI 配置持久化服务。
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _profilesDirectory;
    private readonly ILogService _logService;

    /// <summary>
    /// 初始化配置服务并准备配置档案目录。
    /// </summary>
    /// <param name="settings">应用设置选项。</param>
    public ConfigurationService(IOptions<AppSettings> settings, ILogService logService)
    {
        _logService = logService;
        var dataRoot = PathHelper.ResolveDataRoot(settings.Value.DataRoot);
        _profilesDirectory = Path.Combine(dataRoot, "profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    /// <summary>
    /// 加载指定配置档案的 ComfyUI 配置。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>规范化后的 ComfyUI 配置对象。</returns>
    public async Task<ComfyConfiguration> LoadConfigurationAsync(string profileId)
    {
        var profile = await LoadProfileAsync(profileId);
        return NormalizeConfiguration(profile?.Configuration ?? new ComfyConfiguration());
    }

    /// <summary>
    /// 保存指定配置档案的 ComfyUI 配置。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <param name="configuration">待保存的配置对象。</param>
    public async Task SaveConfigurationAsync(string profileId, ComfyConfiguration configuration)
    {
        ComfyConfigurationValidator.ThrowIfInvalid(configuration);
        var profile = await LoadProfileAsync(profileId) ?? new Profile { Id = profileId, Name = profileId };
        profile.Configuration = configuration;
        profile.LastModified = DateTime.Now;
        if (profile.CreatedAt == default)
        {
            profile.CreatedAt = DateTime.Now;
        }

        await SaveProfileAsync(profile);
    }

    /// <summary>
    /// 校验配置中的关键字段是否合法。
    /// </summary>
    /// <param name="configuration">待校验的配置对象。</param>
    /// <returns>配置合法时返回 true，否则返回 false。</returns>
    public Task<bool> ValidateConfigurationAsync(ComfyConfiguration configuration)
    {
        return Task.FromResult(ComfyConfigurationValidator.Validate(configuration).Count == 0);
    }

    /// <summary>
    /// 读取指定配置档案文件。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>反序列化后的配置档案；不存在时返回 null。</returns>
    private async Task<Profile?> LoadProfileAsync(string profileId)
    {
        var filePath = GetProfilePath(profileId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        using var document = await JsonDocument.ParseAsync(stream);
        var profile = document.RootElement.Deserialize<Profile>(_serializerOptions);
        if (profile != null && document.RootElement.TryGetProperty("Configuration", out var config)
            && config.TryGetProperty("Miscellaneous", out var misc)
            && !misc.TryGetProperty("FastMode", out _)
            && profile.Configuration.Miscellaneous.FastOptions.Count > 0)
        {
            profile.Configuration.Miscellaneous.FastMode = FastMode.Selected;
            _logService.Log($"[配置迁移] {profileId}：旧快速优化列表已迁移为指定选项模式。", GUILogLevel.Warning);
        }
        return profile;
    }

    /// <summary>
    /// 将配置档案写入磁盘。
    /// </summary>
    /// <param name="profile">待保存的配置档案。</param>
    private async Task SaveProfileAsync(Profile profile)
    {
        var filePath = GetProfilePath(profile.Id);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, _serializerOptions);
    }

    /// <summary>
    /// 获取配置档案文件路径。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>配置档案对应的 JSON 文件路径。</returns>
    private string GetProfilePath(string profileId)
    {
        return Path.Combine(_profilesDirectory, $"{profileId}.json");
    }

    /// <summary>
    /// 规范化配置中的路径字段，移除已失效的目录引用。
    /// </summary>
    /// <param name="configuration">待规范化的配置对象。</param>
    /// <returns>规范化后的配置对象。</returns>
    private ComfyConfiguration NormalizeConfiguration(ComfyConfiguration configuration)
    {
        if (configuration.Memory.VramMode == VramMode.NormalVram)
        {
            configuration.Memory.VramMode = VramMode.Auto;
            _logService.Log("[配置迁移] NormalVram 已被 ComfyUI 移除，已改为自动显存模式。", GUILogLevel.Warning);
        }
        if (configuration.Device.DisableIpexOptimize)
        {
            configuration.Device.DisableIpexOptimize = false;
            _logService.Log("[配置迁移] ComfyUI 已移除 IPEX 优化开关，该设置已清除。", GUILogLevel.Warning);
        }
        if (configuration.Miscellaneous.DisableApiNodes)
        {
            configuration.Miscellaneous.DisableApiNodes = false;
            configuration.Miscellaneous.Offline = true;
            _logService.Log("[配置迁移] 禁用 API 节点已迁为离线模式，同时限制前端联网并禁用 Partner 节点。", GUILogLevel.Warning);
        }
        if (configuration.Miscellaneous.Verbose is ComfyLogLevel.LegacyTrace or ComfyLogLevel.LegacyNone)
        {
            configuration.Miscellaneous.Verbose = configuration.Miscellaneous.Verbose == ComfyLogLevel.LegacyTrace
                ? ComfyLogLevel.Debug : ComfyLogLevel.Information;
            _logService.Log("[配置迁移] 旧日志级别已转换为 ComfyUI 支持的级别。", GUILogLevel.Warning);
        }
        var extraModelBaseDirectory = configuration.Paths.ExtraModelBaseDirectory;
        if (!string.IsNullOrWhiteSpace(extraModelBaseDirectory) && !Directory.Exists(extraModelBaseDirectory))
        {
            configuration.Paths.ExtraModelBaseDirectory = null;
        }

        if (configuration.Launch.DisableAutoLaunch)
        {
            configuration.Launch.AutoLaunch = false;
        }

        if (configuration.Memory.DisableAsyncOffload)
        {
            configuration.Memory.AsyncOffload = false;
            configuration.Memory.AsyncOffloadStreams = null;
        }

        return configuration;
    }
}
