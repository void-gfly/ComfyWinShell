namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 管理 ComfyUI-Manager 的用户级配置。
/// </summary>
public interface IComfyManagerSettingsService
{
    /// <summary>
    /// 同步 ComfyUI-Manager 远程安装自定义节点相关开关。
    /// </summary>
    /// <param name="comfyUiPath">ComfyUI 核心目录。</param>
    /// <param name="userDirectory">ComfyUI 用户目录；为空时使用默认 user 目录。</param>
    /// <param name="enabled">是否允许远程安装自定义节点。</param>
    Task ApplyRemoteCustomNodeInstallAsync(string comfyUiPath, string? userDirectory, bool enabled);
}
