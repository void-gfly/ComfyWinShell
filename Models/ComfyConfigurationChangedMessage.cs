using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfDesktop.Models;

/// <summary>
/// 表示 ComfyUI 配置保存后发布的消息。
/// </summary>
public sealed class ComfyConfigurationChangedMessage : ValueChangedMessage<string>
{
    /// <summary>
    /// 初始化 ComfyUI 配置变更消息。
    /// </summary>
    /// <param name="value">变更后的配置档案 ID。</param>
    public ComfyConfigurationChangedMessage(string value) : base(value)
    {
    }
}
