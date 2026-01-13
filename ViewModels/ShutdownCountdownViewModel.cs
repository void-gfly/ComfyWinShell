using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfDesktop.ViewModels;

public partial class ShutdownCountdownViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private int _remainingSeconds;

    public ShutdownCountdownViewModel(int remainingSeconds)
    {
        _statusText = "正在优雅停止 ComfyUI...";
        _remainingSeconds = remainingSeconds;
    }
}
