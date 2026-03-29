using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// ComfyUI 配置读写服务接口。
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// 加载指定配置档案的 ComfyUI 配置。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>规范化后的 ComfyUI 配置对象。</returns>
    Task<ComfyConfiguration> LoadConfigurationAsync(string profileId);

    /// <summary>
    /// 保存指定配置档案的 ComfyUI 配置。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <param name="configuration">要保存的 ComfyUI 配置。</param>
    Task SaveConfigurationAsync(string profileId, ComfyConfiguration configuration);

    /// <summary>
    /// 校验 ComfyUI 配置是否合法。
    /// </summary>
    /// <param name="configuration">待校验的 ComfyUI 配置。</param>
    /// <returns>配置合法时返回 true，否则返回 false。</returns>
    Task<bool> ValidateConfigurationAsync(ComfyConfiguration configuration);
}
