using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// ComfyUI 运行环境检测服务实现
/// </summary>
public sealed class EnvironmentCheckService : IEnvironmentCheckService
{
    private readonly ILogService _logService;

    public event EventHandler<EnvironmentCheckEventArgs>? CheckProgressUpdated;

    public EnvironmentCheckService(ILogService logService)
    {
        _logService = logService;
    }

    public async Task<EnvironmentCheckResult> CheckAllAsync(string? pythonPath = null, CancellationToken cancellationToken = default)
    {
        var result = new EnvironmentCheckResult();

        _logService.Log("========================================");
        _logService.Log("开始 ComfyUI 环境检测");
        _logService.Log("========================================");

        // 1. Python 版本检测
        result.PythonVersion = await CheckPythonVersionAsync(pythonPath, cancellationToken);
        ReportProgress(result.PythonVersion);

        // 2. Git 可用性检测
        result.GitAvailable = await CheckGitAsync(cancellationToken);
        ReportProgress(result.GitAvailable);

        // 3. PyTorch 可用性检测
        result.PyTorchAvailable = await CheckPyTorchAsync(pythonPath, cancellationToken);
        ReportProgress(result.PyTorchAvailable);

        // 4. CUDA 支持检测
        result.CudaSupport = await CheckCudaAsync(pythonPath, cancellationToken);
        ReportProgress(result.CudaSupport);

        // 5. AMD ROCm 支持检测
        result.AmdSupport = await CheckAmdAsync(pythonPath, cancellationToken);
        ReportProgress(result.AmdSupport);

        // 6. FFmpeg 可用性检测
        result.FFmpegAvailable = await CheckFFmpegAsync(cancellationToken);
        ReportProgress(result.FFmpegAvailable);

        _logService.Log("========================================");
        if (result.AllRequiredPassed)
        {
            _logService.Log("环境检测完成 - ✓ 所有必需项通过", GUILogLevel.Success);
        }
        else
        {
            _logService.LogError("环境检测完成 - ✗ 存在未通过项");
        }
        _logService.Log("========================================");

        return result;
    }

    /// <summary>
    /// 检测 Python 版本（必需：>= 3.10）
    /// </summary>
    private async Task<CheckItemResult> CheckPythonVersionAsync(string? pythonPath, CancellationToken cancellationToken)
    {
        var result = new CheckItemResult { Name = "Python 版本" };
        _logService.Log("[检测] Python 版本...");

        try
        {
            var pythonExe = GetPythonExecutable(pythonPath);
            var (exitCode, output) = await RunCommandAsync(pythonExe, "--version", cancellationToken);

            if (exitCode != 0)
            {
                result.Status = CheckStatus.Error;
                result.Message = "Python 不可用";
                result.Detail = output;
                _logService.LogError($"  ✗ Python 不可用: {output}");
                return result;
            }

            // 解析版本号（输出格式：Python 3.10.11）
            var versionMatch = System.Text.RegularExpressions.Regex.Match(output, @"Python\s+(\d+)\.(\d+)\.(\d+)");
            if (!versionMatch.Success)
            {
            result.Status = CheckStatus.Warning;
            result.Message = "无法解析 Python 版本";
            result.Detail = output;
            _logService.Log($"  ⚠ 无法解析版本号: {output}", GUILogLevel.Warning);
            return result;
            }

            var major = int.Parse(versionMatch.Groups[1].Value);
            var minor = int.Parse(versionMatch.Groups[2].Value);
            var patch = int.Parse(versionMatch.Groups[3].Value);
            var version = $"{major}.{minor}.{patch}";

            // 检查版本要求（>= 3.10）
            if (major < 3 || (major == 3 && minor < 10))
            {
                result.Status = CheckStatus.Error;
                result.Message = $"Python 版本过低: {version}（需要 >= 3.10）";
                result.Detail = output;
                _logService.LogError($"  ✗ Python 版本过低: {version}（需要 >= 3.10）");
                return result;
            }

            result.Status = CheckStatus.Success;
            result.Message = $"Python {version}";
            result.Detail = output;
            _logService.Log($"  ✓ Python {version}", GUILogLevel.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Error;
            result.Message = "Python 检测异常";
            result.Detail = ex.Message;
            _logService.LogError($"  ✗ Python 检测异常: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// 检测 Git 可用性（必需）
    /// </summary>
    private async Task<CheckItemResult> CheckGitAsync(CancellationToken cancellationToken)
    {
        var result = new CheckItemResult { Name = "Git" };
        _logService.Log("[检测] Git 可用性...");

        try
        {
            var (exitCode, output) = await RunCommandAsync("git", "--version", cancellationToken);

            if (exitCode != 0)
            {
                result.Status = CheckStatus.Error;
                result.Message = "Git 不可用";
                result.Detail = output;
                _logService.LogError($"  ✗ Git 不可用: {output}");
                return result;
            }

            result.Status = CheckStatus.Success;
            result.Message = output.Split('\n')[0].Trim();
            result.Detail = output;
            _logService.Log($"  ✓ {result.Message}", GUILogLevel.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Error;
            result.Message = "Git 检测异常";
            result.Detail = ex.Message;
            _logService.LogError($"  ✗ Git 检测异常: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// 检测 PyTorch 可用性（必需）
    /// </summary>
    private async Task<CheckItemResult> CheckPyTorchAsync(string? pythonPath, CancellationToken cancellationToken)
    {
        var result = new CheckItemResult { Name = "PyTorch" };
        _logService.Log("[检测] PyTorch 可用性...");

        try
        {
            var pythonExe = GetPythonExecutable(pythonPath);
            var script = "import torch; print(f'PyTorch {torch.__version__}')";
            var (exitCode, output) = await RunCommandAsync(pythonExe, $"-c \"{script}\"", cancellationToken);

            if (exitCode != 0)
            {
                result.Status = CheckStatus.Error;
                result.Message = "PyTorch 未安装或不可用";
                result.Detail = output;
                _logService.LogError($"  ✗ PyTorch 未安装: {output}");
                return result;
            }

            result.Status = CheckStatus.Success;
            result.Message = output.Split('\n')[0].Trim();
            result.Detail = output;
            _logService.Log($"  ✓ {result.Message}", GUILogLevel.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Error;
            result.Message = "PyTorch 检测异常";
            result.Detail = ex.Message;
            _logService.LogError($"  ✗ PyTorch 检测异常: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// 检测 CUDA 支持（可选）
    /// </summary>
    private async Task<CheckItemResult> CheckCudaAsync(string? pythonPath, CancellationToken cancellationToken)
    {
        var result = new CheckItemResult { Name = "CUDA 支持" };
        _logService.Log("[检测] CUDA 支持...");

        try
        {
            // 方法 1: 尝试 nvidia-smi
            var (exitCode1, output1) = await RunCommandAsync("nvidia-smi", "--query-gpu=driver_version,name --format=csv,noheader", cancellationToken, timeoutSeconds: 5);
            if (exitCode1 == 0 && !string.IsNullOrWhiteSpace(output1))
            {
                result.Status = CheckStatus.Success;
                result.Message = $"NVIDIA GPU 可用";
                result.Detail = $"nvidia-smi 输出:\n{output1}";
                _logService.Log($"  ✓ NVIDIA GPU 可用", GUILogLevel.Success);
                _logService.Log($"    {output1.Trim()}");

                // 进一步检测 PyTorch CUDA
                await CheckPyTorchCudaAsync(result, pythonPath, cancellationToken);
                return result;
            }

            // 方法 2: 使用 PyTorch 检测 CUDA
            var pythonExe = GetPythonExecutable(pythonPath);
            var script = "import torch; print('CUDA available' if torch.cuda.is_available() else 'CUDA not available'); " +
                         "print(f'Device count: {torch.cuda.device_count()}') if torch.cuda.is_available() else None; " +
                         "print(f'Device name: {torch.cuda.get_device_name(0)}') if torch.cuda.is_available() else None";
            
            var (exitCode2, output2) = await RunCommandAsync(pythonExe, $"-c \"{script}\"", cancellationToken, timeoutSeconds: 10);

            if (exitCode2 == 0 && output2.Contains("CUDA available"))
            {
                result.Status = CheckStatus.Success;
                result.Message = "PyTorch CUDA 可用";
                result.Detail = output2;
                _logService.Log($"  ✓ PyTorch CUDA 可用", GUILogLevel.Success);
                _logService.Log($"    {output2.Trim().Replace("\n", "\n    ")}");
                return result;
            }

            // CUDA 不可用（不是错误，只是警告）
            result.Status = CheckStatus.Warning;
            result.Message = "CUDA 不可用（将使用 CPU 模式）";
            result.Detail = $"nvidia-smi: {output1}\nPyTorch CUDA: {output2}";
            _logService.Log($"  ⚠ CUDA 不可用（将使用 CPU 模式）", GUILogLevel.Warning);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Warning;
            result.Message = "CUDA 检测异常（非必需）";
            result.Detail = ex.Message;
            _logService.Log($"  ⚠ CUDA 检测异常: {ex.Message}", GUILogLevel.Warning);
            return result;
        }
    }

    /// <summary>
    /// 检测 PyTorch CUDA 可用性（辅助方法）
    /// </summary>
    private async Task CheckPyTorchCudaAsync(CheckItemResult result, string? pythonPath, CancellationToken cancellationToken)
    {
        try
        {
            var pythonExe = GetPythonExecutable(pythonPath);
            var script = "import torch; print('CUDA available' if torch.cuda.is_available() else 'CUDA not available'); " +
                         "print(f'PyTorch CUDA: {torch.version.cuda}') if torch.cuda.is_available() else None";
            
            var (exitCode, output) = await RunCommandAsync(pythonExe, $"-c \"{script}\"", cancellationToken, timeoutSeconds: 10);
            if (exitCode == 0)
            {
                result.Detail += $"\n\nPyTorch CUDA 检测:\n{output}";
                if (output.Contains("CUDA available"))
                {
                    _logService.Log($"    PyTorch CUDA: 可用", GUILogLevel.Success);
                }
                else
                {
                    _logService.Log($"    PyTorch CUDA: 不可用（可能未安装 CUDA 版本的 PyTorch）", GUILogLevel.Warning);
                }
            }
        }
        catch
        {
            // 忽略 PyTorch CUDA 检测失败
        }
    }

    /// <summary>
    /// 检测 AMD ROCm 支持（可选）
    /// </summary>
    private async Task<CheckItemResult> CheckAmdAsync(string? pythonPath, CancellationToken cancellationToken)
    {
        var result = new CheckItemResult { Name = "AMD ROCm 支持" };
        _logService.Log("[检测] AMD ROCm 支持...");

        try
        {
            // 方法 1: 尝试 rocm-smi
            var (exitCode1, output1) = await RunCommandAsync("rocm-smi", "--showproductname", cancellationToken, timeoutSeconds: 5);
            if (exitCode1 == 0 && !string.IsNullOrWhiteSpace(output1))
            {
                result.Status = CheckStatus.Success;
                result.Message = "AMD GPU (ROCm) 可用";
                result.Detail = output1;
                _logService.Log($"  ✓ AMD GPU (ROCm) 可用", GUILogLevel.Success);
                _logService.Log($"    {output1.Trim().Replace("\n", "\n    ")}");
                return result;
            }

            // 方法 2: 使用 PyTorch 检测 ROCm
            var pythonExe = GetPythonExecutable(pythonPath);
            var script = "import torch; " +
                         "hip_available = hasattr(torch.version, 'hip') and torch.version.hip is not None; " +
                         "print('ROCm available' if hip_available else 'ROCm not available'); " +
                         "print(f'HIP version: {torch.version.hip}') if hip_available else None";
            
            var (exitCode2, output2) = await RunCommandAsync(pythonExe, $"-c \"{script}\"", cancellationToken, timeoutSeconds: 10);

            if (exitCode2 == 0 && output2.Contains("ROCm available"))
            {
                result.Status = CheckStatus.Success;
                result.Message = "PyTorch ROCm 可用";
                result.Detail = output2;
                _logService.Log($"  ✓ PyTorch ROCm 可用", GUILogLevel.Success);
                _logService.Log($"    {output2.Trim().Replace("\n", "\n    ")}");
                return result;
            }

            // ROCm 不可用（不是错误，只是警告）
            result.Status = CheckStatus.Warning;
            result.Message = "ROCm 不可用（将使用 CUDA 或 CPU 模式）";
            result.Detail = $"rocm-smi: {output1}\nPyTorch ROCm: {output2}";
            _logService.Log($"  ⚠ ROCm 不可用", GUILogLevel.Warning);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Warning;
            result.Message = "ROCm 检测异常（非必需）";
            result.Detail = ex.Message;
            _logService.Log($"  ⚠ ROCm 检测异常: {ex.Message}", GUILogLevel.Warning);
            return result;
        }
    }

    /// <summary>
    /// 检测 FFmpeg 可用性（可选）
    /// </summary>
    private async Task<CheckItemResult> CheckFFmpegAsync(CancellationToken cancellationToken)
    {
        var result = new CheckItemResult { Name = "FFmpeg" };
        _logService.Log("[检测] FFmpeg 可用性...");

        try
        {
            var (exitCode, output) = await RunCommandAsync("ffmpeg", "-version", cancellationToken, timeoutSeconds: 5);

            if (exitCode != 0)
            {
                result.Status = CheckStatus.Warning;
                result.Message = "FFmpeg 不可用（视频处理功能受限）";
                result.Detail = output;
                _logService.Log($"  ⚠ FFmpeg 不可用（视频处理功能受限）", GUILogLevel.Warning);
                return result;
            }

            // 提取版本信息（第一行）
            var versionLine = output.Split('\n')[0].Trim();
            result.Status = CheckStatus.Success;
            result.Message = versionLine;
            result.Detail = output;
            _logService.Log($"  ✓ {versionLine}", GUILogLevel.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Warning;
            result.Message = "FFmpeg 检测异常（非必需）";
            result.Detail = ex.Message;
            _logService.Log($"  ⚠ FFmpeg 检测异常: {ex.Message}", GUILogLevel.Warning);
            return result;
        }
    }

    /// <summary>
    /// 获取 Python 可执行文件路径
    /// </summary>
    private static string GetPythonExecutable(string? pythonPath)
    {
        if (!string.IsNullOrWhiteSpace(pythonPath))
        {
            // 如果提供了 Python 路径
            if (File.Exists(pythonPath))
            {
                return pythonPath;
            }
            if (Directory.Exists(pythonPath))
            {
                var pythonExe = Path.Combine(pythonPath, "python.exe");
                if (File.Exists(pythonExe))
                {
                    return pythonExe;
                }
            }
        }

        // 使用系统 PATH 中的 python
        return "python";
    }

    /// <summary>
    /// 运行命令行工具
    /// </summary>
    private static async Task<(int exitCode, string output)> RunCommandAsync(
        string command, 
        string arguments, 
        CancellationToken cancellationToken,
        int timeoutSeconds = 30)
    {
        var output = new StringBuilder();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            await process.WaitForExitAsync(cts.Token);

            return (process.ExitCode, output.ToString());
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // 忽略终止进程失败
            }
            return (-1, $"命令执行超时（{timeoutSeconds}秒）");
        }
        catch (Exception ex)
        {
            return (-1, $"命令执行失败: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>
    /// 报告检测进度
    /// </summary>
    private void ReportProgress(CheckItemResult result)
    {
        CheckProgressUpdated?.Invoke(this, new EnvironmentCheckEventArgs
        {
            ItemName = result.Name,
            Status = result.Status,
            Message = result.Message
        });
    }
}
