using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 用当前 ComfyUI 的 Python 执行 pip install -U comfyui-manager。
/// </summary>
public sealed class ComfyManagerUpgradeService : IComfyManagerUpgradeService
{
    private readonly IComfyPathService _comfyPathService;
    private readonly IPythonPathService _pythonPathService;
    private readonly IExternalProcessRunner _processRunner;
    private readonly ILogService _logService;

    public ComfyManagerUpgradeService(
        IComfyPathService comfyPathService,
        IPythonPathService pythonPathService,
        IExternalProcessRunner processRunner,
        ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _pythonPathService = pythonPathService;
        _processRunner = processRunner;
        _logService = logService;
    }

    public async Task<ComfyManagerUpgradeResult> UpgradeAsync(CancellationToken cancellationToken = default)
    {
        _comfyPathService.Refresh();
        if (!_comfyPathService.IsValid || string.IsNullOrWhiteSpace(_comfyPathService.ComfyRootPath))
        {
            return ComfyManagerUpgradeResult.Fail(_comfyPathService.ErrorMessage ?? "未找到有效的 ComfyUI 安装目录。");
        }

        _pythonPathService.Resolve(_comfyPathService.ComfyRootPath);
        if (!_pythonPathService.IsValid || string.IsNullOrWhiteSpace(_pythonPathService.PythonPath))
        {
            return ComfyManagerUpgradeResult.Fail("未能定位 ComfyUI 使用的 Python 环境。");
        }

        const string arguments = "-m pip install -U comfyui-manager";
        var pythonPath = _pythonPathService.PythonPath;
        _logService.Log($"升级 ComfyUI-Manager: {pythonPath} {arguments}");

        var result = await _processRunner.RunAsync(new ExternalProcessCommand
        {
            FileName = pythonPath,
            Arguments = arguments,
            WorkingDirectory = _comfyPathService.ComfyRootPath
        }, cancellationToken);

        if (result.Success)
        {
            _logService.Log("ComfyUI-Manager 升级完成", GUILogLevel.Success);
            return ComfyManagerUpgradeResult.Ok();
        }

        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        if (string.IsNullOrWhiteSpace(details))
        {
            details = $"ExitCode {result.ExitCode}";
        }

        _logService.LogError($"升级 ComfyUI-Manager 失败: {details}");
        return ComfyManagerUpgradeResult.Fail($"升级 ComfyUI-Manager 失败: {details}");
    }
}
