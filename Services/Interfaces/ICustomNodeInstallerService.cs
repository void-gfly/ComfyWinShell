using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 安装 ComfyUI 自定义节点的服务接口。
/// </summary>
public interface ICustomNodeInstallerService
{
    /// <summary>
    /// 从 GitHub 仓库地址安装自定义节点。
    /// </summary>
    /// <param name="repositoryUrl">GitHub 仓库地址。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>安装结果。</returns>
    Task<CustomNodeInstallResult> InstallAsync(string repositoryUrl, CancellationToken cancellationToken = default);
}
