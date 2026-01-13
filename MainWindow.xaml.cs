using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using WpfDesktop.Services.Interfaces;
using WpfDesktop.ViewModels;
using WpfDesktop.Views;

namespace WpfDesktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IProcessService _processService;
        private bool _allowClose;
        private bool _shutdownInProgress;

        public MainWindow(MainViewModel viewModel, IProcessService processService)
        {
            _processService = processService;

            Debug.WriteLine($"MainWindow 构造函数: viewModel={viewModel != null}");
            Debug.WriteLine($"MainWindow 构造函数: CurrentView={viewModel?.CurrentView?.GetType().Name ?? "null"}");
            
            InitializeComponent();
            DataContext = viewModel;
            
            Debug.WriteLine($"MainWindow DataContext 设置完成: {DataContext != null}");
        }

        private async void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            if (_shutdownInProgress)
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = true;
            _shutdownInProgress = true;

            await ShutdownComfyUiAsync();
        }

        private async Task ShutdownComfyUiAsync()
        {
            var status = await _processService.GetStatusAsync();
            if (status == null || !status.IsRunning)
            {
                _allowClose = true;
                await Dispatcher.BeginInvoke(Close);
                return;
            }

            var viewModel = new ShutdownCountdownViewModel(20);
            var dialog = new ShutdownCountdownDialog
            {
                DataContext = viewModel,
                Owner = this
            };

            dialog.Show();

            await _processService.RequestGracefulStopAsync();

            var exited = await WaitForExitWithCountdownAsync(viewModel, TimeSpan.FromSeconds(20));
            if (!exited)
            {
                viewModel.StatusText = "未退出，正在强制关闭...";
                await _processService.StopAsync();
                await _processService.WaitForExitAsync(TimeSpan.FromSeconds(5));
            }

            dialog.Close();
            _allowClose = true;
            await Dispatcher.BeginInvoke(Close);
        }

        private async Task<bool> WaitForExitWithCountdownAsync(ShutdownCountdownViewModel viewModel, TimeSpan timeout)
        {
            var remainingSeconds = (int)Math.Ceiling(timeout.TotalSeconds);
            for (var remaining = remainingSeconds; remaining > 0; remaining--)
            {
                viewModel.RemainingSeconds = remaining;
                var exited = await _processService.WaitForExitAsync(TimeSpan.FromSeconds(1));
                if (exited)
                {
                    viewModel.StatusText = "ComfyUI 已停止，正在退出...";
                    return true;
                }
            }

            viewModel.RemainingSeconds = 0;
            return await _processService.WaitForExitAsync(TimeSpan.Zero);
        }
    }
}
