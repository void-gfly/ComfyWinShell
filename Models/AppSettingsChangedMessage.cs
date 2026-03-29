using CommunityToolkit.Mvvm.Messaging.Messages;
using WpfDesktop.Models;

namespace WpfDesktop.Models;

/// <summary>
/// 表示应用设置变更后发布的消息。
/// </summary>
public class AppSettingsChangedMessage : ValueChangedMessage<AppSettings>
{
    /// <summary>
    /// 初始化应用设置变更消息。
    /// </summary>
    /// <param name="value">变更后的应用设置实例。</param>
    public AppSettingsChangedMessage(AppSettings value) : base(value)
    {
    }
}
