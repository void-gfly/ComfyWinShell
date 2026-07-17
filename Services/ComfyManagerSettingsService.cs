using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 维护 ComfyUI-Manager 的 config.ini 中由本程序暴露的安全开关。
/// </summary>
public sealed class ComfyManagerSettingsService : IComfyManagerSettingsService
{
    private const string DefaultSection = "[default]";
    private const string AllowGitUrlInstallKey = "allow_git_url_install";
    private const string AllowPipInstallKey = "allow_pip_install";

    public async Task ApplyRemoteCustomNodeInstallAsync(string comfyUiPath, string? userDirectory, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(comfyUiPath))
        {
            throw new ArgumentException("ComfyUI path cannot be empty.", nameof(comfyUiPath));
        }

        var configPath = await GetManagerConfigPathAsync(comfyUiPath, userDirectory);
        if (!enabled && !File.Exists(configPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var lines = File.Exists(configPath)
            ? (await File.ReadAllLinesAsync(configPath, Encoding.UTF8)).ToList()
            : new List<string>();

        UpsertDefaultKey(lines, AllowGitUrlInstallKey, enabled);
        UpsertDefaultKey(lines, AllowPipInstallKey, enabled);

        await File.WriteAllLinesAsync(configPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static async Task<string> GetManagerConfigPathAsync(string comfyUiPath, string? userDirectory)
    {
        var rootUserDirectory = string.IsNullOrWhiteSpace(userDirectory)
            ? Path.Combine(comfyUiPath, "user")
            : userDirectory;

        var managerDirectory = await HasSystemUserDirectoryApiAsync(comfyUiPath)
            ? Path.Combine(rootUserDirectory, "__manager")
            : Path.Combine(rootUserDirectory, "default", "ComfyUI-Manager");

        return Path.Combine(managerDirectory, "config.ini");
    }

    private static async Task<bool> HasSystemUserDirectoryApiAsync(string comfyUiPath)
    {
        var folderPathsFile = Path.Combine(comfyUiPath, "folder_paths.py");
        if (!File.Exists(folderPathsFile))
        {
            return false;
        }

        var source = await File.ReadAllTextAsync(folderPathsFile, Encoding.UTF8);
        return Regex.IsMatch(
            source,
            @"^\s*def\s+get_system_user_directory\s*\(",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    private static void UpsertDefaultKey(List<string> lines, string key, bool enabled)
    {
        var sectionIndex = FindDefaultSectionIndex(lines);
        if (sectionIndex < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add(DefaultSection);
            sectionIndex = lines.Count - 1;
        }

        var insertIndex = lines.Count;
        for (var i = sectionIndex + 1; i < lines.Count; i++)
        {
            if (IsSectionHeader(lines[i]))
            {
                insertIndex = i;
                break;
            }

            if (IsKeyLine(lines[i], key))
            {
                lines[i] = $"{key} = {enabled.ToString().ToLowerInvariant()}";
                return;
            }
        }

        lines.Insert(insertIndex, $"{key} = {enabled.ToString().ToLowerInvariant()}");
    }

    private static int FindDefaultSectionIndex(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsSectionHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('[') && trimmed.EndsWith(']');
    }

    private static bool IsKeyLine(string line, string key)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#') || trimmed.StartsWith(';'))
        {
            return false;
        }

        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex < 0)
        {
            return false;
        }

        return string.Equals(trimmed[..equalsIndex].Trim(), key, StringComparison.OrdinalIgnoreCase);
    }
}
