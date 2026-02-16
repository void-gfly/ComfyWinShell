using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class SettingsService : ISettingsService
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly string _settingsFilePath;
    private AppSettings _current;

    public SettingsService(AppSettings settings)
    {
        _current = settings;
        var dataRoot = PathHelper.ResolveDataRoot(settings.DataRoot);
        Directory.CreateDirectory(dataRoot);
        _settingsFilePath = Path.Combine(dataRoot, "settings.json");
    }

    public AppSettings Current => _current;

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

    public async Task SaveAsync(AppSettings settings)
    {
        _current = settings;
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions);
    }

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
