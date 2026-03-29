using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 应用设置读写服务。
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _settingsFilePath;
    private AppSettings _current;

    /// <summary>
    /// 初始化设置服务并准备设置文件路径。
    /// </summary>
    /// <param name="settings">当前应用设置。</param>
    public SettingsService(AppSettings settings)
    {
        _current = settings;
        var dataRoot = PathHelper.ResolveDataRoot(settings.DataRoot);
        Directory.CreateDirectory(dataRoot);
        _settingsFilePath = Path.Combine(dataRoot, "settings.json");
    }

    public AppSettings Current => _current;

    /// <summary>
    /// 从磁盘加载应用设置，并对旧配置执行规范化修正。
    /// </summary>
    /// <returns>当前有效的应用设置对象。</returns>
    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return _current;
        }

        var json = await File.ReadAllTextAsync(_settingsFilePath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions);
        if (settings != null)
        {
            _current = settings;
        }

        // 兼容旧配置：缺少新字段时自动补默认值并回写
        var lineHeightExists = HasTopLevelProperty(json, "LogLineHeight");
        var normalized = NormalizeSettings(_current);
        if (!lineHeightExists || normalized)
        {
            await SaveAsync(_current);
        }

        return _current;
    }

    /// <summary>
    /// 将应用设置保存到磁盘。
    /// </summary>
    /// <param name="settings">待保存的应用设置。</param>
    public async Task SaveAsync(AppSettings settings)
    {
        _current = settings;
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions);
    }

    /// <summary>
    /// 判断 JSON 根对象中是否包含指定顶层属性。
    /// </summary>
    /// <param name="json">JSON 文本。</param>
    /// <param name="propertyName">要查找的属性名。</param>
    /// <returns>存在时返回 true，否则返回 false。</returns>
    private static bool HasTopLevelProperty(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// 规范化设置中的兼容字段，并返回是否发生改动。
    /// </summary>
    /// <param name="settings">待规范化的设置对象。</param>
    /// <returns>发生字段修正时返回 true，否则返回 false。</returns>
    private static bool NormalizeSettings(AppSettings settings)
    {
        var changed = false;

        // 仅允许三档预设值，其他值回退为默认
        if (settings.LogLineHeight is not (12 or 15 or 18))
        {
            settings.LogLineHeight = 15;
            changed = true;
        }

        return changed;
    }
}
