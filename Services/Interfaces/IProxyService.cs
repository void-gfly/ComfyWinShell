namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 代理和镜像服务接口
/// </summary>
public interface IProxyService
{
    /// <summary>
    /// 是否启用代理
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 获取代理服务器地址
    /// </summary>
    string GetProxyServer();

    /// <summary>
    /// 配置进程启动信息以使用代理（通过环境变量）
    /// </summary>
    /// <param name="startInfo">进程启动信息</param>
    void ConfigureProcessProxy(System.Diagnostics.ProcessStartInfo startInfo);

    /// <summary>
    /// 转换 GitHub URL 为镜像 URL（如果启用）
    /// </summary>
    /// <param name="originalUrl">原始 GitHub URL</param>
    /// <returns>镜像 URL 或原始 URL</returns>
    string ConvertGitHubUrl(string originalUrl);

    /// <summary>
    /// 检查是否启用 GitHub 镜像
    /// </summary>
    bool IsGitHubMirrorEnabled { get; }

    /// <summary>
    /// 检查是否启用 pip 镜像
    /// </summary>
    bool IsPipMirrorEnabled { get; }

    /// <summary>
    /// 获取 pip 镜像源配置参数
    /// </summary>
    /// <returns>pip 命令行参数（如 "-i https://..."）</returns>
    string GetPipMirrorArgs();
}
