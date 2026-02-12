using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using WpfDesktop.ViewModels;

namespace WpfDesktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private ILogService? _logService;

        public IHost? AppHost => _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 必须先调用 base.OnStartup 以加载 App.xaml 中的资源
            base.OnStartup(e);

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<AppSettings>(context.Configuration.GetSection("AppSettings"));
                    var appSettings = context.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();
                    services.AddSingleton(appSettings);

                    services.AddSingleton<ArgumentBuilder>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<ILogService, LogService>();
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<IProfileService, ProfileService>();
                    services.AddSingleton<IVersionService, VersionService>();
                    services.AddSingleton<IComfyPathService, ComfyPathService>();
                    services.AddSingleton<IPythonPathService, PythonPathService>();
                    services.AddSingleton<IProxyService, ProxyService>();
                    services.AddSingleton<IGitService, GitService>();
                    services.AddSingleton<IProcessService, ProcessService>();
                    services.AddSingleton<IHardwareMonitorService, HardwareMonitorService>();
                    services.AddSingleton<IResourceService, ResourceService>();
                    services.AddSingleton<IEnvironmentCheckService, EnvironmentCheckService>();
                    services.AddSingleton<IWorkflowAnalyzerService, WorkflowAnalyzerService>();
                    services.AddSingleton<IWorkflowPackagerService, WorkflowPackagerService>();

                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<ConfigurationViewModel>();
                    services.AddSingleton<VersionManagerViewModel>();
                    services.AddSingleton<ProfileManagerViewModel>();
                    services.AddSingleton<ProcessMonitorViewModel>();
                    services.AddSingleton<HardwareMonitorViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<ResourcesViewModel>();
                    services.AddSingleton<MainViewModel>();

                    services.AddSingleton<MainWindow>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddDebug();
                })
                .Build();

            _host.Start();

            // 获取 LogService 用于全局异常处理
            _logService = _host.Services.GetRequiredService<ILogService>();

            // 添加全局异常处理 - 输出到日志而不是弹框
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Debug.WriteLine($"未处理异常: {args.ExceptionObject}");
                _logService?.LogError("未处理异常", ex);
            };
            DispatcherUnhandledException += (s, args) =>
            {
                Debug.WriteLine($"Dispatcher异常: {args.Exception}");
                _logService?.LogError("Dispatcher异常", args.Exception);
                args.Handled = true;
            };

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                // 应用退出时优先停止 ComfyUI，并强制执行一次残留进程清理
                try
                {
                    var processService = _host.Services.GetRequiredService<IProcessService>();
                    processService.StopAsync().Wait(TimeSpan.FromSeconds(3));

                    var comfyPathService = _host.Services.GetRequiredService<IComfyPathService>();
                    comfyPathService.Refresh();

                    var appSettings = _host.Services.GetRequiredService<AppSettings>();
                    var cleanupRoot = ResolveCleanupRootPath(comfyPathService, appSettings);
                    if (!string.IsNullOrWhiteSpace(cleanupRoot))
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        processService.CleanupLingeringProcessesAsync(cleanupRoot, cts.Token).Wait(TimeSpan.FromSeconds(4));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"退出时清理 ComfyUI 进程失败: {ex}");
                    _logService?.LogError("退出时清理 ComfyUI 进程失败", ex);
                }

                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
                _host = null;
            }

            base.OnExit(e);
        }

        private static string? ResolveCleanupRootPath(IComfyPathService comfyPathService, AppSettings appSettings)
        {
            if (comfyPathService.IsValid && !string.IsNullOrWhiteSpace(comfyPathService.ComfyRootPath))
            {
                return comfyPathService.ComfyRootPath;
            }

            if (!string.IsNullOrWhiteSpace(appSettings.PythonRoot))
            {
                var pythonRootParent = Path.GetDirectoryName(appSettings.PythonRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(pythonRootParent))
                {
                    return pythonRootParent;
                }
            }

            return null;
        }
    }
}
