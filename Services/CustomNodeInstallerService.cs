using System.IO;
using System.Text.RegularExpressions;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 按 ComfyUI 自定义节点流程安装 GitHub 节点仓库。
/// </summary>
public sealed class CustomNodeInstallerService : ICustomNodeInstallerService
{
    private static readonly Regex GitHubRepoRegex = new(
        @"^https://github\.com/(?<owner>[^/\s]+)/(?<repo>[^/\s]+?)(?:\.git)?(?:/tree/(?<ref>.+))?/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IComfyPathService _comfyPathService;
    private readonly IPythonPathService _pythonPathService;
    private readonly IProxyService _proxyService;
    private readonly IExternalProcessRunner _processRunner;
    private readonly ILogService _logService;

    public CustomNodeInstallerService(
        IComfyPathService comfyPathService,
        IPythonPathService pythonPathService,
        IProxyService proxyService,
        IExternalProcessRunner processRunner,
        ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _pythonPathService = pythonPathService;
        _proxyService = proxyService;
        _processRunner = processRunner;
        _logService = logService;
    }

    public async Task<CustomNodeInstallResult> InstallAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var parsed = ParseGitHubRepositoryUrl(repositoryUrl);
        if (parsed == null)
        {
            return CustomNodeInstallResult.Fail("请输入有效的 GitHub 仓库地址。");
        }

        _comfyPathService.Refresh();
        if (!_comfyPathService.IsValid ||
            string.IsNullOrWhiteSpace(_comfyPathService.ComfyUiPath) ||
            string.IsNullOrWhiteSpace(_comfyPathService.ComfyRootPath))
        {
            return CustomNodeInstallResult.Fail(_comfyPathService.ErrorMessage ?? "未找到有效的 ComfyUI 安装目录。");
        }

        var customNodesPath = Path.Combine(_comfyPathService.ComfyUiPath, "custom_nodes");
        Directory.CreateDirectory(customNodesPath);

        var nodePath = Path.Combine(customNodesPath, parsed.Value.RepositoryName);
        if (Directory.Exists(nodePath))
        {
            return CustomNodeInstallResult.Fail($"自定义节点目录已存在: {nodePath}", parsed.Value.RepositoryName, nodePath);
        }

        _pythonPathService.Resolve(_comfyPathService.ComfyRootPath);
        if (!_pythonPathService.IsValid || string.IsNullOrWhiteSpace(_pythonPathService.PythonPath))
        {
            return CustomNodeInstallResult.Fail("未能定位 ComfyUI 使用的 Python 环境。", parsed.Value.RepositoryName, nodePath);
        }

        var cloneUrl = _proxyService.ConvertGitHubUrl(parsed.Value.CloneUrl);
        _logService.Log($"开始安装自定义节点: {parsed.Value.RepositoryName}");

        var cloneResult = await RunRequiredCommandAsync(
            "git",
            $"clone {QuoteArgument(cloneUrl)} {QuoteArgument(parsed.Value.RepositoryName)}",
            customNodesPath,
            "Git clone 失败",
            cancellationToken);
        if (!cloneResult.Success)
        {
            return CustomNodeInstallResult.Fail(cloneResult.ErrorMessage, parsed.Value.RepositoryName, nodePath);
        }

        if (!string.IsNullOrWhiteSpace(parsed.Value.Reference))
        {
            var checkoutResult = await RunRequiredCommandAsync(
                "git",
                $"checkout {QuoteArgument(parsed.Value.Reference)}",
                nodePath,
                "Git checkout 失败",
                cancellationToken);
            if (!checkoutResult.Success)
            {
                return CustomNodeInstallResult.Fail(checkoutResult.ErrorMessage, parsed.Value.RepositoryName, nodePath);
            }
        }

        var requirementsPath = Path.Combine(nodePath, "requirements.txt");
        if (File.Exists(requirementsPath))
        {
            var pipResult = await RunRequiredCommandAsync(
                _pythonPathService.PythonPath,
                $"-m pip install -r {QuoteArgument(requirementsPath)}",
                _comfyPathService.ComfyRootPath,
                "安装 requirements.txt 失败",
                cancellationToken);
            if (!pipResult.Success)
            {
                return CustomNodeInstallResult.Fail(pipResult.ErrorMessage, parsed.Value.RepositoryName, nodePath);
            }
        }

        var installPyPath = Path.Combine(nodePath, "install.py");
        if (File.Exists(installPyPath))
        {
            var installResult = await RunRequiredCommandAsync(
                _pythonPathService.PythonPath,
                "install.py",
                nodePath,
                "执行 install.py 失败",
                cancellationToken);
            if (!installResult.Success)
            {
                return CustomNodeInstallResult.Fail(installResult.ErrorMessage, parsed.Value.RepositoryName, nodePath);
            }
        }

        _logService.Log($"自定义节点安装完成: {parsed.Value.RepositoryName}", GUILogLevel.Success);
        return CustomNodeInstallResult.Ok(parsed.Value.RepositoryName, nodePath);
    }

    private async Task<(bool Success, string ErrorMessage)> RunRequiredCommandAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        string failurePrefix,
        CancellationToken cancellationToken)
    {
        _logService.Log($"{failurePrefix.Replace("失败", string.Empty).Trim()}: {fileName} {arguments}");

        var command = new ExternalProcessCommand
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory
        };

        var result = await _processRunner.RunAsync(command, cancellationToken);
        if (result.Success)
        {
            return (true, string.Empty);
        }

        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        if (string.IsNullOrWhiteSpace(details))
        {
            details = $"ExitCode {result.ExitCode}";
        }

        _logService.LogError($"{failurePrefix}: {details}");
        return (false, $"{failurePrefix}: {details}");
    }

    private static ParsedRepository? ParseGitHubRepositoryUrl(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return null;
        }

        var match = GitHubRepoRegex.Match(repositoryUrl.Trim());
        if (!match.Success)
        {
            return null;
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            return null;
        }

        var cloneUrl = $"https://github.com/{owner}/{repo}.git";
        var reference = match.Groups["ref"].Success ? Uri.UnescapeDataString(match.Groups["ref"].Value.Trim('/')) : string.Empty;
        return new ParsedRepository(repo, cloneUrl, reference);
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private readonly record struct ParsedRepository(string RepositoryName, string CloneUrl, string Reference);
}
