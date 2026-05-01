using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using WpfDesktop.Models;
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
        private readonly MainViewModel _viewModel;
        private bool _allowClose;
        private bool _shutdownInProgress;
        private TaskbarIcon? _trayIcon;
        private MenuItem? _appNameMenuItem;
        private MenuItem? _serviceMenuItem;

        public MainWindow(MainViewModel viewModel, IProcessService processService)
        {
            _processService = processService;
            _viewModel = viewModel;

            Debug.WriteLine($"MainWindow 构造函数: viewModel={viewModel != null}");
            Debug.WriteLine($"MainWindow 构造函数: CurrentView={viewModel?.CurrentView?.GetType().Name ?? "null"}");
            
            InitializeComponent();
            DataContext = viewModel;
            
            Debug.WriteLine($"MainWindow DataContext 设置完成: {DataContext != null}");

            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            // 优先从文件加载，失败则从 exe 嵌入图标提取
            System.Drawing.Icon? icon = null;
            try
            {
                var icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (System.IO.File.Exists(icoPath))
                {
                    icon = new System.Drawing.Icon(icoPath);
                }
                else
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null)
                        icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载托盘图标失败: {ex.Message}");
            }

            // 显示窗口
            _appNameMenuItem = new MenuItem { IsEnabled = false };
            UpdateTrayAppName(_viewModel.AppName, _viewModel.AppVersionText);

            var showMenuItem = new MenuItem { Header = "显示窗口" };
            showMenuItem.Click += (_, _) => ShowWindowFromTray();

            // 服务子菜单项
            _serviceMenuItem = new MenuItem { Header = "启动" };
            _serviceMenuItem.Click += async (_, _) => await _viewModel.ToggleProcessCommand.ExecuteAsync(null);

            var serviceParentMenuItem = new MenuItem { Header = "ComfyUI 服务" };
            serviceParentMenuItem.Items.Add(_serviceMenuItem);

            // 退出
            var exitMenuItem = new MenuItem { Header = "退出" };
            exitMenuItem.Click += async (_, _) => await TrayExitAsync();

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(_appNameMenuItem);
            contextMenu.Items.Add(showMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(serviceParentMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitMenuItem);

            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "ComfyShell",
                ContextMenu = contextMenu,
                Icon = icon
            };
            UpdateTrayAppName(_viewModel.AppName, _viewModel.AppVersionText);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindowFromTray();

            // 通过代码创建（非 XAML）时必须调用 ForceCreate，否则图标不会出现在系统托盘
            _trayIcon.ForceCreate();

            _processService.StatusChanged += OnProcessStatusChanged;
        }

        private void OnProcessStatusChanged(object? sender, ProcessStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                if (_serviceMenuItem == null) return;
                _serviceMenuItem.Header = status.IsRunning
                    ? $"停止 ({_viewModel.CurrentEndpointText})"
                    : "启动";
            });
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(MainViewModel.AppName), StringComparison.Ordinal)
                && !string.Equals(e.PropertyName, nameof(MainViewModel.AppVersionText), StringComparison.Ordinal))
            {
                return;
            }

            Dispatcher.Invoke(() => UpdateTrayAppName(_viewModel.AppName, _viewModel.AppVersionText));
        }

        private void UpdateTrayAppName(string? appName, string? appVersionText)
        {
            var displayName = string.IsNullOrWhiteSpace(appName) ? "ComfyShell" : appName;
            var versionText = string.IsNullOrWhiteSpace(appVersionText) ? string.Empty : appVersionText.Trim();

            if (_appNameMenuItem != null)
            {
                _appNameMenuItem.Header = string.IsNullOrWhiteSpace(versionText)
                    ? displayName
                    : $"{displayName} {versionText}";
            }

            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = displayName;
            }
        }

        private void ShowWindowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private async Task TrayExitAsync()
        {
            ShowWindowFromTray();

            if (_shutdownInProgress) return;

            _shutdownInProgress = true;
            try
            {
                await ShutdownComfyUiAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关闭流程异常，强制退出窗口: {ex}");
                _allowClose = true;
                await Dispatcher.BeginInvoke(Close);
            }
            finally
            {
                _shutdownInProgress = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _processService.StatusChanged -= OnProcessStatusChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _trayIcon?.Dispose();
            _trayIcon = null;
            base.OnClosed(e);
            Application.Current.Shutdown();
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

            // 点击关闭按钮时隐藏到系统托盘
            e.Cancel = true;
            Hide();
        }

        private async Task ShutdownComfyUiAsync()
        {
            ShutdownCountdownDialog? dialog = null;
            var shouldCloseWindow = false;
            var viewModel = new ShutdownCountdownViewModel(0)
            {
                StatusText = "正在检查 ComfyUI 进程状态...",
                HintText = "请稍候，正在准备退出流程。"
            };

            try
            {
                dialog = new ShutdownCountdownDialog
                {
                    DataContext = viewModel,
                    Owner = this
                };
                dialog.Show();

                // 先把窗口渲染出来，再执行可能耗时的状态检查，避免“点关闭后无反馈”的卡顿感
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                var status = await _processService.GetStatusAsync();
                if (status == null || !status.IsRunning)
                {
                    shouldCloseWindow = true;
                    return;
                }

                viewModel.ProcessInfoText = status.ProcessId.HasValue
                    ? $"目标进程 PID: {status.ProcessId.Value}"
                    : "目标进程 PID: 未知";
                viewModel.RemainingSeconds = 20;

                await _processService.RequestGracefulStopAsync();

                var exited = await WaitForExitWithCountdownAsync(
                    viewModel,
                    "正在优雅停止 ComfyUI...",
                    "请保持窗口开启，正在等待进程自行退出。",
                    TimeSpan.FromSeconds(20));

                if (!exited)
                {
                    viewModel.StatusText = "未退出，正在强制关闭...";
                    viewModel.HintText = "已发出强制结束指令，请稍候。";
                    await _processService.StopAsync();

                    exited = await WaitForExitWithCountdownAsync(
                        viewModel,
                        "正在强制关闭 ComfyUI...",
                        "如果倒计时结束仍未退出，将自动打开任务管理器。",
                        TimeSpan.FromSeconds(10));

                    if (!exited)
                    {
                        OpenTaskManager();
                        viewModel.StatusText = "进程疑似卡住，已打开任务管理器";
                        viewModel.HintText = "请按 PID 查找并结束该进程。结束后可再次关闭主窗口。";

                        exited = await WaitForExitWithCountdownAsync(
                            viewModel,
                            "等待手动结束进程...",
                            "任务管理器已打开：若已结束进程，窗口会自动退出。",
                            TimeSpan.FromSeconds(30));
                    }
                }

                shouldCloseWindow = exited;

                if (!exited)
                {
                    var processInfo = string.IsNullOrWhiteSpace(viewModel.ProcessInfoText)
                        ? "目标进程 PID: 未知"
                        : viewModel.ProcessInfoText;

                    MessageBox.Show(
                        $"{processInfo}\n进程在自动与强制阶段后仍未退出。\n请在任务管理器结束该进程后，再关闭主窗口。",
                        "检测到退出卡住",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            finally
            {
                try
                {
                    dialog?.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"关闭倒计时窗口失败: {ex}");
                }

                if (shouldCloseWindow)
                {
                    _allowClose = true;
                    await Dispatcher.BeginInvoke(Close);
                }
            }
        }

        private async Task<bool> WaitForExitWithCountdownAsync(
            ShutdownCountdownViewModel viewModel,
            string statusText,
            string hintText,
            TimeSpan timeout)
        {
            var remainingSeconds = (int)Math.Ceiling(timeout.TotalSeconds);
            for (var remaining = remainingSeconds; remaining > 0; remaining--)
            {
                viewModel.StatusText = statusText;
                viewModel.HintText = hintText;
                viewModel.RemainingSeconds = remaining;
                var exited = await _processService.WaitForExitAsync(TimeSpan.FromSeconds(1));
                if (exited)
                {
                    viewModel.StatusText = "ComfyUI 已停止，正在退出...";
                    viewModel.HintText = "关闭流程已完成。";
                    return true;
                }
            }

            viewModel.RemainingSeconds = 0;
            return await _processService.WaitForExitAsync(TimeSpan.Zero);
        }

        private static void OpenTaskManager()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskmgr.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"打开任务管理器失败: {ex}");
            }
        }
    }
}
