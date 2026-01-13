using CommunityToolkit.Mvvm.Messaging.Messages;
using WpfDesktop.Models;

namespace WpfDesktop.Models;

public class AppSettingsChangedMessage : ValueChangedMessage<AppSettings>
{
    public AppSettingsChangedMessage(AppSettings value) : base(value)
    {
    }
}
