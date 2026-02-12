using System.IO;
using System.Text.Json;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// ComfyUI 资源管理服务实现
/// </summary>
public class ResourceService : IResourceService
{
    private readonly IComfyPathService _comfyPathService;
    private readonly ILogService _logService;
    private static readonly string DescriptionsFilePath = Path.Combine(AppContext.BaseDirectory, "Resources", "model_descriptions.json");

    // 缓存模型描述
    private IReadOnlyDictionary<string, string>? _modelDescriptionsCache;

    public ResourceService(IComfyPathService comfyPathService, ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _logService = logService;
    }

    public async Task<IReadOnlyList<CustomNodeInfo>> GetCustomNodesAsync()
    {
        return await Task.Run(() =>
        {
            var result = new List<CustomNodeInfo>();

            if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
                return result;

            var customNodesPath = Path.Combine(_comfyPathService.ComfyUiPath, "custom_nodes");
            if (!Directory.Exists(customNodesPath))
                return result;

            var directories = Directory.GetDirectories(customNodesPath);
            foreach (var dir in directories)
            {
                var name = Path.GetFileName(dir);
                // 跳过 __pycache__ 等特殊目录
                if (name.StartsWith("__") || name.StartsWith("."))
                    continue;

                var info = new CustomNodeInfo
                {
                    Name = name,
                    Path = dir,
                    IsGitRepo = Directory.Exists(Path.Combine(dir, ".git"))
                };

                // 尝试读取 Git 远程 URL
                if (info.IsGitRepo)
                {
                    info.GitRemoteUrl = TryGetGitRemoteUrl(dir);
                }

                result.Add(info);
            }

            return result.OrderBy(x => x.Name).ToList();
        });
    }

    public async Task<IReadOnlyList<ModelFolderInfo>> GetModelFoldersAsync()
    {
        return await Task.Run(() =>
        {
            var result = new List<ModelFolderInfo>();

            if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
                return result;

            var modelsPath = Path.Combine(_comfyPathService.ComfyUiPath, "models");
            if (!Directory.Exists(modelsPath))
                return result;

            var directories = Directory.GetDirectories(modelsPath);
            foreach (var dir in directories)
            {
                var name = Path.GetFileName(dir);
                // 跳过隐藏目录
                if (name.StartsWith("."))
                    continue;

                result.Add(new ModelFolderInfo
                {
                    Name = name,
                    Path = dir,
                    SizeBytes = 0, // 初始为 0，后台计算
                    FileCount = 0
                });
            }

            return result.OrderBy(x => x.Name).ToList();
        });
    }

    public async Task<IReadOnlyList<ModelFolderInfo>> GetExtraModelFoldersAsync()
    {
        return await Task.Run(() =>
        {
            var result = new List<ModelFolderInfo>();

            if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
                return result;

            var yamlPath = Path.Combine(_comfyPathService.ComfyUiPath, "extra_model_paths.yaml");
            if (!File.Exists(yamlPath))
                return result;

            var mappedFolders = ParseExtraModelPathsYaml(yamlPath);
            foreach (var (name, path) in mappedFolders)
            {
                if (!Directory.Exists(path))
                    continue;

                result.Add(new ModelFolderInfo
                {
                    Name = name,
                    Path = path,
                    SizeBytes = 0,
                    FileCount = 0
                });
            }

            return result
                .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();
        });
    }

    public async Task<IReadOnlyList<WorkflowInfo>> GetWorkflowsAsync()
    {
        return await Task.Run(() =>
        {
            var result = new List<WorkflowInfo>();

            if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
                return result;

            var workflowsPath = Path.Combine(_comfyPathService.ComfyUiPath, "user", "default", "workflows");
            if (!Directory.Exists(workflowsPath))
                return result;

            var files = Directory.GetFiles(workflowsPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                result.Add(new WorkflowInfo
                {
                    Name = Path.GetFileName(file),
                    Path = file,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime
                });
            }

            return result.OrderByDescending(x => x.LastModified).ToList();
        });
    }

    // 模型文件统计时忽略的扩展名
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".png", ".jpg", ".jpeg", ".log", ".md"
    };

    public async Task<(long SizeBytes, int FileCount)> CalculateFolderSizeAsync(string folderPath)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(folderPath))
                return (0, 0);

            long totalSize = 0;
            int fileCount = 0;

            try
            {
                var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var extension = Path.GetExtension(file);
                        var fileInfo = new FileInfo(file);

                        // 磁盘占用统计所有文件
                        totalSize += fileInfo.Length;

                        // 文件数只统计模型文件（忽略文档、图片、日志、无后缀文件等）
                        if (!string.IsNullOrEmpty(extension) && !IgnoredExtensions.Contains(extension))
                        {
                            fileCount++;
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logService.LogError($"无权访问文件: {file}", ex);
                    }
                    catch (IOException ex)
                    {
                        _logService.LogError($"文件读取错误: {file}", ex);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logService.LogError($"无权访问目录: {folderPath}", ex);
            }
            catch (IOException ex)
            {
                _logService.LogError($"目录访问错误: {folderPath}", ex);
            }

            return (totalSize, fileCount);
        });
    }

    private string? TryGetGitRemoteUrl(string repoPath)
    {
        try
        {
            var configPath = Path.Combine(repoPath, ".git", "config");
            if (!File.Exists(configPath))
                return null;

            var lines = File.ReadAllLines(configPath);
            bool inRemoteOrigin = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == "[remote \"origin\"]")
                {
                    inRemoteOrigin = true;
                    continue;
                }

                if (inRemoteOrigin && trimmed.StartsWith("url = "))
                {
                    return trimmed.Substring(6).Trim();
                }

                if (trimmed.StartsWith("[") && inRemoteOrigin)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"解析 Git 配置失败: {repoPath}", ex);
        }

        return null;
    }

    private IReadOnlyList<(string Name, string Path)> ParseExtraModelPathsYaml(string yamlPath)
    {
        var result = new List<(string Name, string Path)>();

        try
        {
            var lines = File.ReadAllLines(yamlPath);
            string? currentSection = null;
            string? basePath = null;

            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                var line = rawLine.Trim();
                if (line.StartsWith("#"))
                    continue;

                if (!rawLine.StartsWith(" ") && line.EndsWith(":"))
                {
                    currentSection = line.TrimEnd(':');
                    basePath = null;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(currentSection))
                    continue;

                if (line.StartsWith("base_path:"))
                {
                    basePath = NormalizeYamlValue(line.Substring("base_path:".Length));
                    continue;
                }

                var keyValueSplit = line.Split(':', 2);
                if (keyValueSplit.Length != 2)
                    continue;

                var key = keyValueSplit[0].Trim();
                var value = NormalizeYamlValue(keyValueSplit[1]);

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || key == "base_path")
                    continue;

                var fullPath = value;
                if (!Path.IsPathRooted(fullPath))
                {
                    if (string.IsNullOrWhiteSpace(basePath))
                        continue;

                    fullPath = Path.Combine(basePath, fullPath);
                }

                result.Add((key, Path.GetFullPath(fullPath)));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"解析 extra_model_paths.yaml 失败: {yamlPath}", ex);
        }

        return result;
    }

    private static string NormalizeYamlValue(string value)
    {
        var normalized = value.Trim();

        var commentIndex = normalized.IndexOf(" #", StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            normalized = normalized.Substring(0, commentIndex).Trim();
        }

        if ((normalized.StartsWith("\"") && normalized.EndsWith("\"")) ||
            (normalized.StartsWith("'") && normalized.EndsWith("'")))
        {
            normalized = normalized.Substring(1, normalized.Length - 2);
        }

        return normalized.Replace('/', Path.DirectorySeparatorChar).Trim();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetModelDescriptionsAsync()
    {
        // 如果已缓存，直接返回
        if (_modelDescriptionsCache != null)
            return _modelDescriptionsCache;

        return await Task.Run(() =>
        {
            var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (File.Exists(DescriptionsFilePath))
                {
                    var json = File.ReadAllText(DescriptionsFilePath);
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (parsed != null)
                    {
                        foreach (var kvp in parsed)
                        {
                            descriptions[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"加载模型描述文件失败: {DescriptionsFilePath}", ex);
            }

            _modelDescriptionsCache = descriptions;
            return (IReadOnlyDictionary<string, string>)descriptions;
        });
    }
}
