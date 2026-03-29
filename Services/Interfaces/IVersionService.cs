using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// ComfyUI 版本管理服务接口。
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// 获取所有已登记版本。
    /// </summary>
    /// <returns>版本列表。</returns>
    Task<IReadOnlyList<ComfyVersion>> GetAllVersionsAsync();
    /// <summary>
    /// 根据标识获取版本信息。
    /// </summary>
    /// <param name="versionId">版本标识。</param>
    /// <returns>找到时返回版本对象，否则返回 null。</returns>
    Task<ComfyVersion?> GetVersionByIdAsync(string versionId);
    /// <summary>
    /// 获取当前激活版本。
    /// </summary>
    /// <returns>激活版本对象；未设置时返回 null。</returns>
    Task<ComfyVersion?> GetActiveVersionAsync();
    /// <summary>
    /// 保存版本信息。
    /// </summary>
    /// <param name="version">待保存的版本对象。</param>
    Task SaveVersionAsync(ComfyVersion version);
    /// <summary>
    /// 删除指定版本。
    /// </summary>
    /// <param name="versionId">版本标识。</param>
    Task DeleteVersionAsync(string versionId);
    /// <summary>
    /// 设置激活版本。
    /// </summary>
    /// <param name="versionId">版本标识。</param>
    /// <returns>设置成功返回 true，否则返回 false。</returns>
    Task<bool> SetActiveVersionAsync(string versionId);
    /// <summary>
    /// 校验版本安装目录是否有效。
    /// </summary>
    /// <param name="version">待校验的版本对象。</param>
    /// <returns>版本有效时返回 true，否则返回 false。</returns>
    Task<bool> ValidateVersionAsync(ComfyVersion version);
    /// <summary>
    /// 将本地目录登记为版本。
    /// </summary>
    /// <param name="path">本地版本目录路径。</param>
    /// <param name="name">可选显示名称。</param>
    /// <returns>创建成功时返回版本对象，否则返回 null。</returns>
    Task<ComfyVersion?> CreateLocalVersionAsync(string path, string? name = null);
}
