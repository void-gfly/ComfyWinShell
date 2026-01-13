using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class VersionService : IVersionService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _versionsFilePath;
    private readonly string _versionsRoot;

    public VersionService(IOptions<AppSettings> settings)
    {
        var dataRoot = Environment.ExpandEnvironmentVariables(settings.Value.DataRoot);
        _versionsRoot = Path.Combine(dataRoot, "versions");
        _versionsFilePath = Path.Combine(dataRoot, "versions.json");
        Directory.CreateDirectory(_versionsRoot);
    }

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

    public async Task<ComfyVersion?> GetVersionByIdAsync(string versionId)
    {
        var versions = await GetAllVersionsAsync();
        return versions.FirstOrDefault(v => v.Id == versionId);
    }

    public async Task<ComfyVersion?> GetActiveVersionAsync()
    {
        var versions = await GetAllVersionsAsync();
        return versions.FirstOrDefault(v => v.IsActive);
    }

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

    public async Task DeleteVersionAsync(string versionId)
    {
        var versions = (await GetAllVersionsAsync()).ToList();
        versions.RemoveAll(v => v.Id == versionId);
        await SaveVersionsAsync(versions);
    }

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

    private async Task SaveVersionsAsync(List<ComfyVersion> versions)
    {
        await using var stream = File.Create(_versionsFilePath);
        await JsonSerializer.SerializeAsync(stream, versions, _serializerOptions);
    }
}
