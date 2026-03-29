using System.Collections.Generic;
using System.Threading.Tasks;
using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// Git 仓库操作服务接口。
/// </summary>
public interface IGitService
{
    /// <summary>
    /// 判断指定路径是否为 Git 仓库。
    /// </summary>
    /// <param name="path">待检查的目录路径。</param>
    /// <returns>是 Git 仓库时返回 true，否则返回 false。</returns>
    Task<bool> IsGitRepositoryAsync(string path);
    /// <summary>
    /// 获取指定仓库的 origin 远程地址。
    /// </summary>
    /// <param name="path">仓库目录路径。</param>
    /// <returns>远程地址字符串。</returns>
    Task<string> GetRemoteUrlAsync(string path);
    /// <summary>
    /// 获取指定仓库当前分支名称。
    /// </summary>
    /// <param name="path">仓库目录路径。</param>
    /// <returns>当前分支名称。</returns>
    Task<string> GetCurrentBranchAsync(string path);
    /// <summary>
    /// 获取指定仓库当前提交哈希。
    /// </summary>
    /// <param name="path">仓库目录路径。</param>
    /// <returns>当前提交哈希。</returns>
    Task<string> GetCurrentCommitHashAsync(string path);
    /// <summary>
    /// 获取指定仓库最近的提交记录。
    /// </summary>
    /// <param name="path">仓库目录路径。</param>
    /// <param name="count">返回的提交数量上限。</param>
    /// <returns>提交记录列表。</returns>
    Task<IReadOnlyList<GitCommit>> GetCommitsAsync(string path, int count = 100);
    /// <summary>
    /// 获取指定仓库的标签列表。
    /// </summary>
    /// <param name="path">仓库目录路径。</param>
    /// <returns>标签对应的提交列表。</returns>
    Task<IReadOnlyList<GitCommit>> GetTagsAsync(string path);
    /// <summary>
    /// 切换指定仓库到目标分支、标签或提交。
    /// </summary>
    /// <param name="path">仓库目录路径。</param>
    /// <param name="refName">目标引用名称。</param>
    Task CheckoutAsync(string path, string refName);
    /// <summary>
    /// 抓取远程仓库最新引用信息。
    /// </summary>
    /// <param name="path">仓库目录路径。</param>
    Task FetchAsync(string path);
}
