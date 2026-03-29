using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 应用设置持久化服务接口。
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 获取当前内存中的应用设置。
    /// </summary>
    AppSettings Current { get; }
    /// <summary>
    /// 从持久化存储加载应用设置。
    /// </summary>
    /// <returns>加载后的应用设置对象。</returns>
    Task<AppSettings> LoadAsync();
    /// <summary>
    /// 保存应用设置到持久化存储。
    /// </summary>
    /// <param name="settings">待保存的应用设置。</param>
    Task SaveAsync(AppSettings settings);
}
