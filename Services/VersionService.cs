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
/// ComfyUI 版本管理服务实现。
/// </summary>
public class VersionService : IVersionService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _versionsFilePath;
    private readonly string _versionsRoot;

    /// <summary>
    /// 初始化版本服务并准备版本数据目录。
    /// </summary>
    /// <param name="settings">应用设置选项。</param>
    public VersionService(IOptions<AppSettings> settings)
    {
        var dataRoot = PathHelper.ResolveDataRoot(settings.Value.DataRoot);
        _versionsRoot = Path.Combine(dataRoot, "versions");
        _versionsFilePath = Path.Combine(dataRoot, "versions.json");
        Directory.CreateDirectory(_versionsRoot);
    }

    /// <summary>
    /// 获取所有已登记版本。
    /// </summary>
    /// <returns>版本列表。</returns>
    public async Task<IReadOnlyList<ComfyVersion>> GetAllVersionsAsync()
    {
        if (!File.Exists(_versionsFilePath))
        {
            return Array.Empty<ComfyVersion>();
        }

        await using var stream = File.OpenRead(_versionsFilePath);
        var versions = await JsonSerializer.DeserializeAsync<List<ComfyVersion>>(stream, _serializerOptions);
        return versions ?? new List<ComfyVersion>();
    }

    /// <summary>
    /// 根据标识获取版本信息。
    /// </summary>
    /// <param name="versionId">版本标识。</param>
    /// <returns>找到时返回版本对象，否则返回 null。</returns>
    public async Task<ComfyVersion?> GetVersionByIdAsync(string versionId)
    {
        var versions = await GetAllVersionsAsync();
        return versions.FirstOrDefault(v => v.Id == versionId);
    }

    /// <summary>
    /// 获取当前激活版本。
    /// </summary>
    /// <returns>激活版本对象；未设置时返回 null。</returns>
    public async Task<ComfyVersion?> GetActiveVersionAsync()
    {
        var versions = await GetAllVersionsAsync();
        return versions.FirstOrDefault(v => v.IsActive);
    }

    /// <summary>
    /// 保存版本信息，已存在则覆盖，不存在则新增。
    /// </summary>
    /// <param name="version">待保存的版本对象。</param>
    public async Task SaveVersionAsync(ComfyVersion version)
    {
        var versions = (await GetAllVersionsAsync()).ToList();
        var existing = versions.FindIndex(v => v.Id == version.Id);
        if (existing >= 0)
        {
            versions[existing] = version;
        }
        else
        {
            versions.Add(version);
        }

        await SaveVersionsAsync(versions);
    }

    /// <summary>
    /// 删除指定版本记录。
    /// </summary>
    /// <param name="versionId">版本标识。</param>
    public async Task DeleteVersionAsync(string versionId)
    {
        var versions = (await GetAllVersionsAsync()).ToList();
        versions.RemoveAll(v => v.Id == versionId);
        await SaveVersionsAsync(versions);
    }

    /// <summary>
    /// 设置激活版本，并更新其最后使用时间。
    /// </summary>
    /// <param name="versionId">目标版本标识。</param>
    /// <returns>找到目标版本时返回 true，否则返回 false。</returns>
    public async Task<bool> SetActiveVersionAsync(string versionId)
    {
        var versions = (await GetAllVersionsAsync()).ToList();
        var found = false;
        foreach (var version in versions)
        {
            version.IsActive = version.Id == versionId;
            if (version.IsActive)
            {
                found = true;
                version.LastUsed = DateTime.Now;
            }
        }

        await SaveVersionsAsync(versions);
        return found;
    }

    /// <summary>
    /// 将指定本地目录创建为本地版本记录。
    /// </summary>
    /// <param name="path">本地版本目录。</param>
    /// <param name="name">可选显示名称。</param>
    /// <returns>创建成功时返回版本对象，否则返回 null。</returns>
    public async Task<ComfyVersion?> CreateLocalVersionAsync(string path, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return null;
        }

        var versionName = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : name;

        var version = new ComfyVersion
        {
            Name = string.IsNullOrWhiteSpace(versionName) ? "本地版本" : versionName,
            Version = "local",
            Type = VersionType.Local,
            InstallPath = path,
            CreatedAt = DateTime.Now
        };

        var isValid = await ValidateVersionAsync(version);
        version.IsCorrupted = !isValid;

        await SaveVersionAsync(version);
        return version;
    }

    /// <summary>
    /// 校验版本安装目录是否包含可用的 ComfyUI 启动文件。
    /// </summary>
    /// <param name="version">待校验的版本对象。</param>
    /// <returns>版本有效时返回 true，否则返回 false。</returns>
    public Task<bool> ValidateVersionAsync(ComfyVersion version)
    {
        if (string.IsNullOrWhiteSpace(version.InstallPath))
        {
            return Task.FromResult(false);
        }

        if (!Directory.Exists(version.InstallPath))
        {
            return Task.FromResult(false);
        }

        var mainPy = Path.Combine(version.InstallPath, "main.py");
        var comfyRoot = Path.Combine(version.InstallPath, "ComfyUI");
        var comfyMainPy = Path.Combine(comfyRoot, "main.py");

        if (File.Exists(mainPy) || File.Exists(comfyMainPy))
        {
            return Task.FromResult(true);
        }

        var scripts = new[]
        {
            "run_nvidia_gpu.bat",
            "run_amd_gpu.bat",
            "run_cpu.bat",
            "run_gpu.bat",
            "run.bat"
        };

        var hasScript = scripts.Any(script => File.Exists(Path.Combine(version.InstallPath, script)));
        return Task.FromResult(hasScript);
    }

    /// <summary>
    /// 将版本列表写入版本清单文件。
    /// </summary>
    /// <param name="versions">待保存的版本列表。</param>
    private async Task SaveVersionsAsync(List<ComfyVersion> versions)
    {
        await using var stream = File.Create(_versionsFilePath);
        await JsonSerializer.SerializeAsync(stream, versions, _serializerOptions);
    }
}
