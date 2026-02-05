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

        await using var stream = File.OpenRead(_settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _serializerOptions);
        if (settings != null)
        {
            _current = settings;
        }

        return _current;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        _current = settings;
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions);
    }
}
