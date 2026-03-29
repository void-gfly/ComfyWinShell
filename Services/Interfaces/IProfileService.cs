using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 配置档案管理服务接口。
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// 获取所有配置档案。
    /// </summary>
    /// <returns>配置档案列表。</returns>
    Task<IReadOnlyList<Profile>> GetProfilesAsync();
    /// <summary>
    /// 根据标识获取单个配置档案。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>找到时返回配置档案，否则返回 null。</returns>
    Task<Profile?> GetProfileAsync(string profileId);
    /// <summary>
    /// 创建新的配置档案。
    /// </summary>
    /// <param name="name">配置档案名称。</param>
    /// <param name="description">配置档案描述。</param>
    /// <returns>新建的配置档案对象。</returns>
    Task<Profile> CreateProfileAsync(string name, string? description = null);
    /// <summary>
    /// 保存配置档案。
    /// </summary>
    /// <param name="profile">待保存的配置档案。</param>
    Task SaveProfileAsync(Profile profile);
    /// <summary>
    /// 删除指定配置档案。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    Task DeleteProfileAsync(string profileId);
    /// <summary>
    /// 将指定配置档案设为默认档案。
    /// </summary>
    /// <param name="profileId">配置档案标识。</param>
    /// <returns>设置成功返回 true，否则返回 false。</returns>
    Task<bool> SetDefaultProfileAsync(string profileId);
    /// <summary>
    /// 从文件导入配置档案。
    /// </summary>
    /// <param name="filePath">导入文件路径。</param>
    /// <returns>导入成功时返回档案对象，否则返回 null。</returns>
    Task<Profile?> ImportProfileAsync(string filePath);
    /// <summary>
    /// 导出配置档案到指定文件。
    /// </summary>
    /// <param name="profile">待导出的配置档案。</param>
    /// <param name="filePath">导出目标文件路径。</param>
    Task ExportProfileAsync(Profile profile, string filePath);
}
