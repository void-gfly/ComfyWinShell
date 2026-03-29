using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
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

    /// <summary>
    /// 初始化配置服务并准备配置档案目录。
    /// </summary>
    /// <param name="settings">应用设置选项。</param>
    public ConfigurationService(IOptions<AppSettings> settings)
    {
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
        if (configuration.Network.Port is < 1 or > 65535)
        {
            return Task.FromResult(false);
        }

        if (!string.IsNullOrWhiteSpace(configuration.Network.TlsKeyFile)
            && !File.Exists(configuration.Network.TlsKeyFile))
        {
            return Task.FromResult(false);
        }

        if (!string.IsNullOrWhiteSpace(configuration.Network.TlsCertFile)
            && !File.Exists(configuration.Network.TlsCertFile))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
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
        return await JsonSerializer.DeserializeAsync<Profile>(stream, _serializerOptions);
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
    private static ComfyConfiguration NormalizeConfiguration(ComfyConfiguration configuration)
    {
        var extraModelBaseDirectory = configuration.Paths.ExtraModelBaseDirectory;
        if (!string.IsNullOrWhiteSpace(extraModelBaseDirectory) && !Directory.Exists(extraModelBaseDirectory))
        {
            configuration.Paths.ExtraModelBaseDirectory = null;
        }

        return configuration;
    }
}
