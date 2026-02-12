using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfDesktop.ViewModels;

public partial class ShutdownCountdownViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private int _remainingSeconds;

    [ObservableProperty]
    private string _processInfoText = string.Empty;

    [ObservableProperty]
    private string _hintText = string.Empty;

    public ShutdownCountdownViewModel(int remainingSeconds, int? processId = null)
    {
        _statusText = "正在优雅停止 ComfyUI...";
        _remainingSeconds = remainingSeconds;
        _processInfoText = processId.HasValue ? $"目标进程 PID: {processId.Value}" : "目标进程 PID: 未知";
        _hintText = "请保持窗口开启，正在处理退出流程。";
    }
}
