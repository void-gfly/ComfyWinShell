using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 配置档案管理服务实现。
/// </summary>
public class ProfileService : IProfileService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _profilesDirectory;

    /// <summary>
    /// 初始化配置档案服务并确保档案目录存在。
    /// </summary>
    /// <param name="settings">应用设置选项。</param>
    public ProfileService(IOptions<AppSettings> settings)
    {
        var dataRoot = PathHelper.ResolveDataRoot(settings.Value.DataRoot);
        _profilesDirectory = Path.Combine(dataRoot, "profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    /// <summary>
    /// 获取所有配置档案。
    /// </summary>
    /// <returns>配置档案列表。</returns>
    public async Task<IReadOnlyList<Profile>> GetProfilesAsync()
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            return Array.Empty<Profile>();
        }

        var profiles = new List<Profile>();
        foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var profile = await JsonSerializer.DeserializeAsync<Profile>(stream, _serializerOptions);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    /// <summary>
    /// 根据标识读取配置档案。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>找到时返回配置档案，否则返回 null。</returns>
    public async Task<Profile?> GetProfileAsync(string profileId)
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
    /// 创建新的配置档案。
    /// </summary>
    /// <param name="name">配置档案名称。</param>
    /// <param name="description">配置档案描述。</param>
    /// <returns>创建后的配置档案对象。</returns>
    public async Task<Profile> CreateProfileAsync(string name, string? description = null)
    {
        var profile = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = string.IsNullOrWhiteSpace(name) ? "新配置" : name,
            Description = description,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now
        };

        await SaveProfileAsync(profile);
        return profile;
    }

    /// <summary>
    /// 将指定档案设为默认配置。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>找到并设置成功时返回 true，否则返回 false。</returns>
    public async Task<bool> SetDefaultProfileAsync(string profileId)
    {
        var profiles = (await GetProfilesAsync()).ToList();
        var found = false;
        foreach (var profile in profiles)
        {
            var isDefault = profile.Id == profileId;
            if (profile.IsDefault != isDefault)
            {
                profile.IsDefault = isDefault;
                profile.LastModified = DateTime.Now;
            }

            if (isDefault)
            {
                found = true;
            }
        }

        foreach (var profile in profiles)
        {
            await SaveProfileAsync(profile);
        }

        return found;
    }

    /// <summary>
    /// 从外部文件导入配置档案。
    /// </summary>
    /// <param name="filePath">导入文件路径。</param>
    /// <returns>导入成功时返回配置档案对象，否则返回 null。</returns>
    public async Task<Profile?> ImportProfileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        var profile = await JsonSerializer.DeserializeAsync<Profile>(stream, _serializerOptions);
        if (profile == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.Id) || File.Exists(GetProfilePath(profile.Id)))
        {
            profile.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = "导入配置";
        }

        profile.CreatedAt = DateTime.Now;
        profile.LastModified = DateTime.Now;

        await SaveProfileAsync(profile);
        return profile;
    }

    /// <summary>
    /// 将配置档案导出到指定文件。
    /// </summary>
    /// <param name="profile">待导出的配置档案。</param>
    /// <param name="filePath">导出目标路径。</param>
    public async Task ExportProfileAsync(Profile profile, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _profilesDirectory);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, _serializerOptions);
    }

    /// <summary>
    /// 保存单个配置档案到磁盘。
    /// </summary>
    /// <param name="profile">待保存的配置档案。</param>
    public async Task SaveProfileAsync(Profile profile)
    {
        profile.LastModified = DateTime.Now;
        if (profile.CreatedAt == default)
        {
            profile.CreatedAt = DateTime.Now;
        }

        var filePath = GetProfilePath(profile.Id);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profile, _serializerOptions);
    }

    /// <summary>
    /// 删除指定配置档案文件。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    public Task DeleteProfileAsync(string profileId)
    {
        var filePath = GetProfilePath(profileId);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取配置档案文件路径。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>对应的 JSON 文件路径。</returns>
    private string GetProfilePath(string profileId)
    {
        return Path.Combine(_profilesDirectory, $"{profileId}.json");
    }
}
