using System.IO;
using System.Linq;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 工作流打包服务实现
/// </summary>
public class WorkflowPackagerService : IWorkflowPackagerService
{
    private static readonly string[] PythonRuntimeDirectoryNames =
    [
        "python_embeded",
        "python_embedded",
        "python",
        "py",
        "python3",
        "python310",
        "python311",
        "python312",
        "py312",
        "python3.12",
        "python313",
        "venv",
        ".venv"
    ];

    private readonly IComfyPathService _comfyPathService;
    private readonly ILogService _logService;

    public WorkflowPackagerService(IComfyPathService comfyPathService, ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _logService = logService;
    }

    public async Task<WorkflowPackageResult> PackageWorkflowAsync(
        WorkflowAnalysisResult analysisResult,
        string targetPath,
        IProgress<string>? progress = null,
        IProgress<double>? progressPercentage = null)
    {
        var result = new WorkflowPackageResult
        {
            TargetPath = targetPath,
            StartTime = DateTime.Now
        };

        try
        {
            // 验证 ComfyUI 路径
            if (!_comfyPathService.IsValid ||
                string.IsNullOrEmpty(_comfyPathService.ComfyUiPath) ||
                string.IsNullOrEmpty(_comfyPathService.ComfyRootPath))
            {
                result.Success = false;
                result.ErrorMessage = "ComfyUI 路径未配置或无效";
                return result;
            }

            var comfyPath = _comfyPathService.ComfyUiPath;
            var comfyRootPath = _comfyPathService.ComfyRootPath;

            // 验证目标目录
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            var packagedComfyPath = Path.Combine(targetPath, "ComfyUI");

            progress?.Report("📂 开始复制 ComfyUI 核心文件...");
            progressPercentage?.Report(10);

            // 第一步：复制 ComfyUI 目录（排除 models 文件夹）
            var filesCopied = await CopyComfyUiFilesAsync(comfyPath, packagedComfyPath, progress);
            result.TotalFilesCopied = filesCopied;

            progress?.Report("🐍 开始复制 Python 运行环境...");
            var pythonFilesCopied = await CopyPythonRuntimeAsync(comfyRootPath, targetPath, progress);
            result.TotalFilesCopied += pythonFilesCopied;

            progressPercentage?.Report(50);
            progress?.Report($"✅ 已复制 {filesCopied} 个 ComfyUI 文件，{pythonFilesCopied} 个 Python 环境文件");

            // 第二步：复制工作流所需的模型
            progress?.Report("📦 开始复制工作流所需模型...");
            var modelsCopied = await CopyRequiredModelsAsync(
                analysisResult.RequiredModels,
                comfyPath,
                packagedComfyPath,
                progress);
            result.TotalModelsCopied = modelsCopied;

            progressPercentage?.Report(90);
            progress?.Report($"✅ 已复制 {modelsCopied} 个模型文件");

            // 第三步：复制工作流文件本身
            progress?.Report("📄 复制工作流文件...");
            await CopyWorkflowFileAsync(analysisResult.WorkflowPath, packagedComfyPath);
            progress?.Report("✅ 工作流文件已复制");

            // 计算打包后的总大小
            progressPercentage?.Report(95);
            result.TotalSizeBytes = CalculateDirectorySize(targetPath);
            var sizeInMB = result.TotalSizeBytes / (1024.0 * 1024.0);
            var sizeInGB = result.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
            var sizeDisplay = sizeInGB >= 1 ? $"{sizeInGB:F2} GB" : $"{sizeInMB:F2} MB";

            progress?.Report($"📊 打包完成，总大小: {sizeDisplay}");
            progressPercentage?.Report(100);

            result.Success = true;
            result.EndTime = DateTime.Now;

            _logService.Log($"工作流打包完成: {result.TotalFilesCopied} 个文件, {result.TotalModelsCopied} 个模型, {sizeDisplay}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            _logService.LogError("工作流打包失败", ex);
        }

        return result;
    }

    public async Task<WorkflowPackageResult> PackageBatchWorkflowsAsync(
        List<WorkflowAnalysisResult> analysisResults,
        string targetPath,
        IProgress<string>? progress = null,
        IProgress<double>? progressPercentage = null)
    {
        var result = new WorkflowPackageResult
        {
            TargetPath = targetPath,
            StartTime = DateTime.Now
        };

        try
        {
            if (analysisResults.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "未提供可打包的工作流分析结果";
                return result;
            }

            // 验证 ComfyUI 路径
            if (!_comfyPathService.IsValid ||
                string.IsNullOrEmpty(_comfyPathService.ComfyUiPath) ||
                string.IsNullOrEmpty(_comfyPathService.ComfyRootPath))
            {
                result.Success = false;
                result.ErrorMessage = "ComfyUI 路径未配置或无效";
                return result;
            }

            var comfyPath = _comfyPathService.ComfyUiPath;
            var comfyRootPath = _comfyPathService.ComfyRootPath;

            // 验证目标目录
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            var packagedComfyPath = Path.Combine(targetPath, "ComfyUI");

            progress?.Report("📂 开始复制 ComfyUI 核心文件...");
            progressPercentage?.Report(10);

            // 第一步：复制 ComfyUI 核心目录（排除 models）
            var filesCopied = await CopyComfyUiFilesAsync(comfyPath, packagedComfyPath, progress);
            result.TotalFilesCopied = filesCopied;
            progress?.Report("🐍 开始复制 Python 运行环境...");
            var pythonFilesCopied = await CopyPythonRuntimeAsync(comfyRootPath, targetPath, progress);
            result.TotalFilesCopied += pythonFilesCopied;
            progressPercentage?.Report(45);
            progress?.Report($"✅ 已复制 {filesCopied} 个 ComfyUI 文件，{pythonFilesCopied} 个 Python 环境文件");

            // 第二步：合并去重模型后复制
            progress?.Report("📦 合并工作流模型依赖...");
            var mergedModels = analysisResults
                .SelectMany(r => r.RequiredModels)
                .Where(m => !string.IsNullOrWhiteSpace(m.ModelPath))
                .GroupBy(m => m.ModelPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            progress?.Report($"📦 去重后模型总数: {mergedModels.Count}");
            var modelsCopied = await CopyRequiredModelsAsync(
                mergedModels,
                comfyPath,
                packagedComfyPath,
                progress);
            result.TotalModelsCopied = modelsCopied;
            progressPercentage?.Report(80);
            progress?.Report($"✅ 已复制 {modelsCopied} 个模型文件");

            // 第三步：复制所有工作流文件
            progress?.Report("📄 开始复制工作流文件...");
            for (var i = 0; i < analysisResults.Count; i++)
            {
                var analysis = analysisResults[i];
                await CopyWorkflowFileAsync(analysis.WorkflowPath, packagedComfyPath);
                result.TotalFilesCopied++;

                progress?.Report($"   ✓ {analysis.WorkflowName}");
                var percentage = 80 + (15.0 * (i + 1) / analysisResults.Count);
                progressPercentage?.Report(percentage);
            }

            // 计算打包后的总大小
            result.TotalSizeBytes = CalculateDirectorySize(targetPath);
            var sizeInMB = result.TotalSizeBytes / (1024.0 * 1024.0);
            var sizeInGB = result.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
            var sizeDisplay = sizeInGB >= 1 ? $"{sizeInGB:F2} GB" : $"{sizeInMB:F2} MB";

            progress?.Report($"📊 批量打包完成，总大小: {sizeDisplay}");
            progressPercentage?.Report(100);

            result.Success = true;
            result.EndTime = DateTime.Now;

            _logService.Log(
                $"批量工作流打包完成: 工作流 {analysisResults.Count} 个, 文件 {result.TotalFilesCopied} 个, 模型 {result.TotalModelsCopied} 个, {sizeDisplay}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            _logService.LogError("批量工作流打包失败", ex);
        }

        return result;
    }

    /// <summary>
    /// 复制 ComfyUI 核心文件（排除 models 目录）
    /// </summary>
    private async Task<int> CopyComfyUiFilesAsync(
        string sourcePath,
        string targetPath,
        IProgress<string>? progress)
    {
        var filesCopied = 0;
        var rootExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "models", "input", "output", "temp"
        };
        var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "__pycache__", ".vscode", ".idea", "venv", ".venv"
        };
        var nestedExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git"
        };

        await Task.Run(() =>
        {
            CopyDirectoryRecursive(sourcePath, targetPath, rootExcludedDirs, excludedDirs, nestedExcludedDirs, depth: 0, ref filesCopied, progress);
        });

        return filesCopied;
    }

    /// <summary>
    /// 复制与 ComfyUI 同级的 Python 运行环境目录，保持原有目录名与层级。
    /// </summary>
    private async Task<int> CopyPythonRuntimeAsync(
        string comfyRootPath,
        string targetPath,
        IProgress<string>? progress)
    {
        var pythonRuntimePath = FindPythonRuntimePath(comfyRootPath);
        if (string.IsNullOrWhiteSpace(pythonRuntimePath))
        {
            progress?.Report("   ⚠ 未检测到可复制的 Python 运行环境目录，已跳过");
            return 0;
        }

        var runtimeDirName = Path.GetFileName(
            pythonRuntimePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetPythonPath = Path.Combine(targetPath, runtimeDirName);
        var filesCopied = 0;
        var rootExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "__pycache__"
        };
        var nestedExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git"
        };

        await Task.Run(() =>
        {
            CopyDirectoryRecursive(pythonRuntimePath, targetPythonPath, rootExcludedDirs, excludedDirs, nestedExcludedDirs, depth: 0, ref filesCopied, progress);
        });

        return filesCopied;
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    private void CopyDirectoryRecursive(
        string sourceDir,
        string targetDir,
        HashSet<string> rootExcludedDirs,
        HashSet<string> excludedDirs,
        HashSet<string> nestedExcludedDirs,
        int depth,
        ref int filesCopied,
        IProgress<string>? progress)
    {
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 复制文件
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);

            try
            {
                File.Copy(file, targetFile, true);
                filesCopied++;

                if (filesCopied % 100 == 0)
                {
                    progress?.Report($"   已复制 {filesCopied} 个文件...");
                }
            }
            catch (Exception ex)
            {
                _logService.Log($"复制文件失败: {file}, {ex.Message}");
            }
        }

        // 递归复制子目录（排除指定目录）
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);

            var isExcluded = excludedDirs.Contains(dirName) ||
                             (depth > 0 && nestedExcludedDirs.Contains(dirName)) ||
                             (depth == 0 && rootExcludedDirs.Contains(dirName));
            if (isExcluded)
            {
                continue;
            }

            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectoryRecursive(dir, targetSubDir, rootExcludedDirs, excludedDirs, nestedExcludedDirs, depth + 1, ref filesCopied, progress);
        }
    }

    /// <summary>
    /// 复制工作流所需的模型（支持来自扩展模型目录的模型）
    /// </summary>
    private async Task<int> CopyRequiredModelsAsync(
        List<RequiredModel> requiredModels,
        string comfyPath,
        string targetPath,
        IProgress<string>? progress)
    {
        var modelsCopied = 0;
        var targetModelsPath = Path.Combine(targetPath, "models");

        await Task.Run(() =>
        {
            foreach (var model in requiredModels.Where(m => m.Exists && !string.IsNullOrEmpty(m.FullPath)))
            {
                try
                {
                    // 使用 ModelPath（逻辑相对路径，如 "checkpoints/model.safetensors"）
                    // 而非从 ComfyUI/models 计算相对路径（扩展目录模型会生成错误的 ..\ 路径）
                    var relativePath = model.ModelPath.Replace('/', Path.DirectorySeparatorChar);

                    // 构建目标路径
                    var targetModelPath = Path.Combine(targetModelsPath, relativePath);
                    var targetModelDir = Path.GetDirectoryName(targetModelPath);

                    if (!string.IsNullOrEmpty(targetModelDir) && !Directory.Exists(targetModelDir))
                    {
                        Directory.CreateDirectory(targetModelDir);
                    }

                    // 复制模型文件
                    File.Copy(model.FullPath!, targetModelPath, true);
                    modelsCopied++;

                    var sizeDisplay = model.SizeBytes < 1024 * 1024 * 1024
                        ? $"{model.SizeBytes / (1024.0 * 1024.0):F1} MB"
                        : $"{model.SizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";

                    progress?.Report($"   ✓ {model.ModelName} ({sizeDisplay})");
                }
                catch (Exception ex)
                {
                    _logService.Log($"复制模型失败: {model.ModelName}, {ex.Message}");
                    progress?.Report($"   ✗ {model.ModelName} (复制失败)");
                }
            }
        });

        return modelsCopied;
    }

    /// <summary>
    /// 复制工作流文件
    /// </summary>
    private async Task CopyWorkflowFileAsync(string workflowPath, string targetPath)
    {
        if (!File.Exists(workflowPath))
        {
            return;
        }

        var workflowFileName = Path.GetFileName(workflowPath);
        var targetWorkflowPath = Path.Combine(targetPath, workflowFileName);

        await Task.Run(() =>
        {
            File.Copy(workflowPath, targetWorkflowPath, true);
        });
    }

    /// <summary>
    /// 计算目录大小
    /// </summary>
    private long CalculateDirectorySize(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            return 0;
        }

        long size = 0;

        try
        {
            var dirInfo = new DirectoryInfo(dirPath);

            // 计算所有文件大小
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"计算目录大小失败: {ex.Message}");
        }

        return size;
    }

    private string? FindPythonRuntimePath(string comfyRootPath)
    {
        if (string.IsNullOrWhiteSpace(comfyRootPath) || !Directory.Exists(comfyRootPath))
        {
            return null;
        }

        foreach (var dirName in PythonRuntimeDirectoryNames)
        {
            var candidatePath = Path.Combine(comfyRootPath, dirName);
            if (HasPythonExecutable(candidatePath))
            {
                return candidatePath;
            }
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(comfyRootPath))
            {
                if (string.Equals(Path.GetFileName(directory), "ComfyUI", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (HasPythonExecutable(directory))
                {
                    return directory;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"检测 Python 运行环境目录失败: {ex.Message}");
        }

        return null;
    }

    private static bool HasPythonExecutable(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        return File.Exists(Path.Combine(directoryPath, "python.exe")) ||
               File.Exists(Path.Combine(directoryPath, "Scripts", "python.exe"));
    }
}
