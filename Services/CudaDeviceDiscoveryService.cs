using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 通过 ComfyUI 使用的 Python 环境读取 torch.cuda 顺序。
/// </summary>
public sealed class CudaDeviceDiscoveryService : ICudaDeviceDiscoveryService
{
    private readonly IComfyPathService _comfyPathService;
    private readonly IPythonPathService _pythonPathService;
    private readonly ILogService _logService;
    private readonly object _syncRoot = new();
    private string? _cachedComfyRootPath;
    private string? _cachedPythonPath;
    private IReadOnlyList<CudaDeviceInfo> _cachedDevices = Array.Empty<CudaDeviceInfo>();

    public CudaDeviceDiscoveryService(
        IComfyPathService comfyPathService,
        IPythonPathService pythonPathService,
        ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _pythonPathService = pythonPathService;
        _logService = logService;
    }

    /// <inheritdoc />
    public IReadOnlyList<CudaDeviceInfo> GetCudaDevices()
    {
        var comfyRootPath = _comfyPathService.ComfyRootPath;
        if (string.IsNullOrWhiteSpace(comfyRootPath))
        {
            return Array.Empty<CudaDeviceInfo>();
        }

        _pythonPathService.Resolve(comfyRootPath);
        var pythonPath = _pythonPathService.PythonPath;

        lock (_syncRoot)
        {
            if (string.Equals(_cachedComfyRootPath, comfyRootPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_cachedPythonPath, pythonPath, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(_cachedPythonPath))
            {
                return _cachedDevices;
            }

            if (string.IsNullOrWhiteSpace(pythonPath))
            {
                _cachedComfyRootPath = comfyRootPath;
                _cachedPythonPath = null;
                _cachedDevices = Array.Empty<CudaDeviceInfo>();
                return _cachedDevices;
            }

            var devices = QueryCudaDevices(pythonPath);
            _cachedComfyRootPath = comfyRootPath;
            _cachedPythonPath = pythonPath;
            _cachedDevices = devices;
            return _cachedDevices;
        }
    }

    private IReadOnlyList<CudaDeviceInfo> QueryCudaDevices(string pythonPath)
    {
        try
        {
            var script = """
import json
import torch

devices = []
if torch.cuda.is_available():
    for index in range(torch.cuda.device_count()):
        devices.append({"deviceId": index, "name": torch.cuda.get_device_name(index)})

print(json.dumps(devices, ensure_ascii=False))
""";

            var output = RunPythonScript(pythonPath, script, timeoutSeconds: 15, out var errorText, out var exitCode);
            if (exitCode != 0)
            {
                _logService.LogError($"读取 torch.cuda 设备列表失败，python 退出码 {exitCode}: {errorText}");
                return Array.Empty<CudaDeviceInfo>();
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                return Array.Empty<CudaDeviceInfo>();
            }

            List<CudaDeviceInfo>? devices = JsonSerializer.Deserialize<List<CudaDeviceInfo>>(output);
            return devices is null ? Array.Empty<CudaDeviceInfo>() : devices;
        }
        catch (Exception ex)
        {
            _logService.LogError("读取 torch.cuda 设备列表失败", ex);
            return Array.Empty<CudaDeviceInfo>();
        }
    }

    private static string RunPythonScript(string pythonPath, string script, int timeoutSeconds, out string errorText, out int exitCode)
    {
        errorText = string.Empty;
        exitCode = -1;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(script);

        if (!process.Start())
        {
            return string.Empty;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // ignore
            }

            return string.Empty;
        }

        Task.WaitAll(stdoutTask, stderrTask);
        exitCode = process.ExitCode;

        var stdout = stdoutTask.Result.Trim();
        errorText = stderrTask.Result.Trim();
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            return stdout;
        }

        return string.Empty;
    }
}
