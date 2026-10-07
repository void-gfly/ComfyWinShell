using CommunityToolkit.Mvvm.ComponentModel;
using WpfDesktop.Models.Enums;

namespace WpfDesktop.Models;

public partial class LogFileConfiguration : ObservableObject
{
    [ObservableProperty]
    private ComfyLogLevel _level = ComfyLogLevel.Information;

    [ObservableProperty]
    private string _path = string.Empty;
}
