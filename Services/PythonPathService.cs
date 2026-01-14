using System.IO;
using System.Text.Json;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class PythonPathService : IPythonPathService
{
    private readonly AppSettings _appSettings;
    private readonly ILogService _logService;
    private string? _pythonPath;

    public PythonPathService(AppSettings appSettings, ILogService logService)
    {
        _appSettings = appSettings;
        _logService = logService;
    }

    public string? PythonPath => _pythonPath;

    public bool IsValid => !string.IsNullOrWhiteSpace(_pythonPath) && File.Exists(_pythonPath);

    public void Resolve(string comfyRootPath)
    {
        // 1. 先检查配置中的 PythonRoot
        if (!string.IsNullOrWhiteSpace(_appSettings.PythonRoot))
        {
            var configuredPath = Path.Combine(_appSettings.PythonRoot, "python.exe");
            if (File.Exists(configuredPath))
            {
                _pythonPath = configuredPath;
                return;
            }
        }

        // 2. 搜索 Python
        _pythonPath = SearchPython(comfyRootPath);

        // 3. 如果找到了，保存到配置
        if (!string.IsNullOrWhiteSpace(_pythonPath) && File.Exists(_pythonPath))
        {
            var pythonRoot = Path.GetDirectoryName(_pythonPath);
            if (!string.IsNullOrWhiteSpace(pythonRoot))
            {
                _appSettings.PythonRoot = pythonRoot;
                SaveSettings();
            }
        }
    }

    private string? SearchPython(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return "python";
        }

        // 1. 常见的固定目录名
        var commonNames = new[] { "python_embeded", "python_embedded", "python", "python3", "python310", "python311", "python312", "python313" };
        foreach (var name in commonNames)
        {
            var pythonExe = Path.Combine(rootPath, name, "python.exe");
            if (File.Exists(pythonExe))
            {
                return pythonExe;
            }
        }

        // 2. 搜索根目录下所有子目录，查找包含 python.exe 的目录
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(rootPath))
            {
                var pythonExe = Path.Combine(dir, "python.exe");
                if (File.Exists(pythonExe))
                {
                    return pythonExe;
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logService.LogError($"无权访问目录: {rootPath}", ex);
        }
        catch (IOException ex)
        {
            _logService.LogError($"目录枚举错误: {rootPath}", ex);
        }

        // 3. 根目录下的 python.exe
        var rootPython = Path.Combine(rootPath, "python.exe");
        if (File.Exists(rootPython))
        {
            return rootPython;
        }

        // 4. 回退到系统 PATH 中的 python
        return "python";
    }

    private void SaveSettings()
    {
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var json = JsonSerializer.Serialize(new { AppSettings = _appSettings }, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            _logService.LogError($"保存配置文件失败: appsettings.json", ex);
        }
    }
}
