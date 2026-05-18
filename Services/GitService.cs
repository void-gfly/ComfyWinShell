using System.Diagnostics;
using System.IO;
using System.Text;
using System.ComponentModel;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// Git 仓库操作服务实现。
/// </summary>
public class GitService : IGitService
{
    private readonly IProxyService _proxyService;
    private readonly ILogService _logService;
    private readonly IResiliencePolicyService _resiliencePolicyService;

    /// <summary>
    /// 初始化 Git 服务。
    /// </summary>
    /// <param name="proxyService">代理服务。</param>
    /// <param name="logService">日志服务。</param>
    public GitService(IProxyService proxyService, ILogService logService, IResiliencePolicyService resiliencePolicyService)
    {
        _proxyService = proxyService;
        _logService = logService;
        _resiliencePolicyService = resiliencePolicyService;
    }

    /// <summary>
    /// 判断指定路径是否为 Git 仓库。
    /// </summary>
    /// <param name="path">待检查目录。</param>
    /// <returns>是 Git 仓库时返回 true，否则返回 false。</returns>
    public async Task<bool> IsGitRepositoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        try
        {
            var result = await RunGitCommandAsync(path, "rev-parse --is-inside-work-tree");
            return result.Trim() == "true";
        }
        catch (Exception ex)
        {
            _logService.LogError($"检测 Git 仓库失败: {path}", ex);
            return false;
        }
    }

    /// <summary>
    /// 获取仓库 origin 远程地址。
    /// </summary>
    /// <param name="path">仓库目录。</param>
    /// <returns>远程地址字符串。</returns>
    public async Task<string> GetRemoteUrlAsync(string path)
    {
        try
        {
            return (await RunGitCommandAsync(path, "remote get-url origin")).Trim();
        }
        catch (Exception ex)
        {
            _logService.LogError($"获取 Git 远程 URL 失败: {path}", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取仓库当前分支名称。
    /// </summary>
    /// <param name="path">仓库目录。</param>
    /// <returns>分支名称；分离头指针时返回 Detached HEAD。</returns>
    public async Task<string> GetCurrentBranchAsync(string path)
    {
        try
        {
            // First try to get branch name
            var branch = (await RunGitCommandAsync(path, "branch --show-current")).Trim();
            if (!string.IsNullOrEmpty(branch))
                return branch;

            // If detached HEAD (e.g. checked out a tag/commit), show the hash or tag
            return "Detached HEAD";
        }
        catch (Exception ex)
        {
            _logService.LogError($"获取当前分支失败: {path}", ex);
            return "Unknown";
        }
    }

    /// <summary>
    /// 获取当前提交哈希。
    /// </summary>
    /// <param name="path">仓库目录。</param>
    /// <returns>完整提交哈希。</returns>
    public async Task<string> GetCurrentCommitHashAsync(string path)
    {
        try
        {
            return (await RunGitCommandAsync(path, "rev-parse HEAD")).Trim();
        }
        catch (Exception ex)
        {
            _logService.LogError($"获取当前提交哈希失败: {path}", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取最近的提交记录。
    /// </summary>
    /// <param name="path">仓库目录。</param>
    /// <param name="count">返回的最大提交数量。</param>
    /// <returns>提交记录列表。</returns>
    public async Task<IReadOnlyList<GitCommit>> GetCommitsAsync(string path, int count = 100)
    {
        var commits = new List<GitCommit>();
        try
        {
            // Format: Hash|ShortHash|Message|Date|Author|Refs
            // Use --no-pager to ensure we get raw output
            var output = await RunGitCommandAsync(path, $"--no-pager log --pretty=format:\"%H|%h|%s|%ai|%an|%D\" -n {count}");
            var currentHash = await GetCurrentCommitHashAsync(path);

            using var reader = new StringReader(output);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var parts = line.Split('|');
                if (parts.Length < 5) continue;

                var commit = new GitCommit
                {
                    Hash = parts[0],
                    ShortHash = parts[1],
                    Message = parts[2],
                    Date = DateTime.TryParse(parts[3], out var date) ? date : DateTime.MinValue,
                    Author = parts[4],
                    IsCurrent = parts[0] == currentHash,
                    Tag = parts.Length > 5 ? ParseTag(parts[5]) : null
                };
                commits.Add(commit);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting commits: {ex.Message}");
            throw; // Re-throw to let ViewModel handle it
        }
        return commits;
    }

    /// <summary>
    /// 获取仓库标签对应的提交列表。
    /// </summary>
    /// <param name="path">仓库目录。</param>
    /// <returns>带标签信息的提交列表。</returns>
    public async Task<IReadOnlyList<GitCommit>> GetTagsAsync(string path)
    {
        var tags = new List<GitCommit>();
        try
        {
            // Get tags sorted by date desc
            // We use git log --tags --simplify-by-decoration to get commits that have tags
            var output = await RunGitCommandAsync(path, "--no-pager log --tags --simplify-by-decoration --pretty=format:\"%H|%h|%s|%ai|%an|%D\"");
            var currentHash = await GetCurrentCommitHashAsync(path);

            using var reader = new StringReader(output);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var parts = line.Split('|');
                if (parts.Length < 6) continue; // Must have refs for tags

                var refString = parts[5];
                var tagName = ParseTag(refString);

                if (string.IsNullOrEmpty(tagName)) continue;

                var commit = new GitCommit
                {
                    Hash = parts[0],
                    ShortHash = parts[1],
                    Message = parts[2],
                    Date = DateTime.TryParse(parts[3], out var date) ? date : DateTime.MinValue,
                    Author = parts[4],
                    IsCurrent = parts[0] == currentHash,
                    Tag = tagName
                };
                tags.Add(commit);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting tags: {ex.Message}");
            throw;
        }
        return tags;
    }

    /// <summary>
    /// 切换仓库到指定引用。
    /// </summary>
    /// <param name="path">仓库目录。</param>
    /// <param name="refName">目标引用名称。</param>
    public async Task CheckoutAsync(string path, string refName)
    {
        await RunGitCommandAsync(path, $"checkout {refName}");
    }

    /// <summary>
    /// 抓取远程仓库最新引用与标签。
    /// </summary>
    /// <param name="path">仓库目录。</param>
    public async Task FetchAsync(string path)
    {
        await RunGitCommandAsync(path, "fetch --all --tags");
    }

    /// <summary>
    /// 从 Git refs 字符串中提取标签名称。
    /// </summary>
    /// <param name="refString">原始 refs 文本。</param>
    /// <returns>提取到的标签名；不存在时返回 null。</returns>
    private string? ParseTag(string refString)
    {
        // Example refs: "HEAD -> master, tag: v1.0, origin/master"
        // We look for "tag: xxx"
        var parts = refString.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "tag:" && i + 1 < parts.Length)
            {
                return parts[i + 1];
            }
        }
        return null;
    }

    /// <summary>
    /// 在指定工作目录执行 Git 命令并返回标准输出。
    /// </summary>
    /// <param name="workingDir">Git 工作目录。</param>
    /// <param name="arguments">Git 命令参数。</param>
    /// <returns>命令标准输出内容。</returns>
    private async Task<string> RunGitCommandAsync(string workingDir, string arguments)
    {
        return await _resiliencePolicyService.ExecuteAsync(
            $"Git:{arguments}",
            async ct =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                // 配置代理环境变量
                _proxyService.ConfigureProcessProxy(startInfo);

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                var started = false;

                process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                try
                {
                    started = process.Start();
                }
                catch (Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 2)
                    {
                        throw new FileNotFoundException("Git executable not found. Please install Git and ensure it is in your PATH.", "git");
                    }

                    throw;
                }

                if (!started)
                {
                    throw new InvalidOperationException("Failed to start Git process.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                try
                {
                    await process.WaitForExitAsync(ct);
                }
                catch
                {
                    if (started && !process.HasExited)
                    {
                        try
                        {
                            process.Kill(entireProcessTree: true);
                        }
                        catch
                        {
                        }
                    }

                    throw;
                }

                if (process.ExitCode != 0)
                {
                    var error = errorBuilder.ToString();
                    throw new Exception($"Git command failed (ExitCode {process.ExitCode}): {error}");
                }

                return outputBuilder.ToString();
            });
    }
}
