using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using WpfDesktop.ViewModels;
using WpfDesktop.Views;

namespace WpfDesktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private ILogService? _logService;
        private int _fatalExceptionHandled;

        public IHost? AppHost => _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 必须先调用 base.OnStartup 以加载 App.xaml 中的资源
            base.OnStartup(e);
            EnsureDefaultAppSettingsFileExists();

            try
            {
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
                RegisterGlobalExceptionHandlers();

                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                HandleFatalException(ex, "应用启动异常");
            }
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

        private static void EnsureDefaultAppSettingsFileExists()
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(settingsPath))
            {
                return;
            }

            var defaultConfig = new
            {
                AppSettings = new AppSettings()
            };

            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(settingsPath, json);
        }

        private void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                HandleFatalException(ex, "未处理异常(AppDomain)");
            };

            DispatcherUnhandledException += (_, args) =>
            {
                args.Handled = true;
                HandleFatalException(args.Exception, "未处理异常(UI线程)");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
                HandleFatalException(args.Exception, "未观察到的任务异常");
            };
        }

        private void HandleFatalException(Exception? exception, string source)
        {
            if (Interlocked.Exchange(ref _fatalExceptionHandled, 1) == 1)
            {
                return;
            }

            try
            {
                _logService?.LogError(source, exception);
            }
            catch
            {
                // 忽略记录日志失败，继续展示异常对话框
            }

            var details = BuildExceptionDetails(exception, source);

            try
            {
                var dispatcher = Current?.Dispatcher;
                if (dispatcher == null)
                {
                    MessageBox.Show(details, "程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else if (dispatcher.CheckAccess())
                {
                    ShowFatalExceptionDialog(details);
                }
                else
                {
                    dispatcher.Invoke(() => ShowFatalExceptionDialog(details));
                }
            }
            catch
            {
                MessageBox.Show(details, "程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try
                {
                    Current?.Shutdown(-1);
                }
                catch
                {
                    Environment.Exit(-1);
                }
            }
        }

        private void ShowFatalExceptionDialog(string details)
        {
            var dialog = new GlobalExceptionDialog(details);
            if (Current?.MainWindow != null)
            {
                dialog.Owner = Current.MainWindow;
            }

            dialog.ShowDialog();
        }

        private static string BuildExceptionDetails(Exception? exception, string source)
        {
            var sb = new StringBuilder();
            sb.AppendLine("程序发生未处理异常。");
            sb.AppendLine("请复制以下信息用于问题排查。");
            sb.AppendLine();
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"来源: {source}");
            sb.AppendLine();

            if (exception == null)
            {
                sb.AppendLine("异常对象为空。");
                return sb.ToString();
            }

            var current = exception;
            var index = 0;
            while (current != null)
            {
                sb.AppendLine($"[{index}] {current.GetType().FullName}");
                sb.AppendLine($"Message: {current.Message}");
                sb.AppendLine("StackTrace:");
                sb.AppendLine(current.StackTrace ?? "(null)");
                sb.AppendLine();
                current = current.InnerException;
                index++;
            }

            return sb.ToString();
        }
    }
}
