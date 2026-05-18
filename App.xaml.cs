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
        private const uint AttachConsoleParentProcess = 0xFFFFFFFF;

        private IHost? _host;
        private ILogService? _logService;
        private AppInstanceLock? _instanceLock;
        private StartupSplashWindow? _startupSplashWindow;
        private int _fatalExceptionHandled;

        public IHost? AppHost => _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 必须先调用 base.OnStartup 以加载 App.xaml 中的资源
            base.OnStartup(e);
            // 隐藏窗口到托盘时不自动退出应用
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            TryAttachParentConsole();
            InitializeFileLogging();

            try
            {
                LogStartupContext(nameof(OnStartup));
                EnsureDefaultAppSettingsFileExists();
                ShowStartupSplash();

                var startupSettings = LoadStartupAppSettings();
                LogStartupSettings(startupSettings);
                _instanceLock = AppInstanceLockHelper.TryAcquire(startupSettings.AppName);
                if (_instanceLock == null)
                {
                    FileLogWriter.Log(nameof(App), "同一个 AppName 的程序实例已经在运行。", GUILogLevel.Warning);
                    CloseStartupSplash();
                    MessageBox.Show("同一个 AppName 的程序实例已经在运行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                Dispatcher.BeginInvoke(new Action(() => _ = InitializeApplicationAsync()), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                CloseStartupSplash();
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
                    if (_logService == null)
                    {
                        FileLogWriter.LogError(nameof(App), "退出时清理 ComfyUI 进程失败", ex);
                    }
                }

                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
                _host = null;
            }

            _instanceLock?.Dispose();
            _instanceLock = null;

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

        private static AppSettings LoadStartupAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();
            var settingsPath = Path.Combine(PathHelper.ResolveDataRoot(appSettings.DataRoot), "settings.json");

            if (!File.Exists(settingsPath))
            {
                return appSettings;
            }

            var json = File.ReadAllText(settingsPath);
            var runtimeSettings = JsonSerializer.Deserialize<AppSettings>(json);
            return runtimeSettings ?? appSettings;
        }

        private void ShowStartupSplash()
        {
            _startupSplashWindow = new StartupSplashWindow();
            _startupSplashWindow.Show();
        }

        private void CloseStartupSplash()
        {
            try
            {
                _startupSplashWindow?.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关闭启动封面失败: {ex}");
            }
            finally
            {
                _startupSplashWindow = null;
            }
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                _host = await Task.Run(CreateAndStartHost);
                FileLogWriter.Log(nameof(App), "主机已启动。", GUILogLevel.Success);

                // 获取 LogService 用于全局异常处理
                _logService = _host.Services.GetRequiredService<ILogService>();
                RegisterGlobalExceptionHandlers();

                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                CloseStartupSplash();
                mainWindow.Show();
                _logService.Log("主窗口已显示。", GUILogLevel.Success);
            }
            catch (Exception ex)
            {
                CloseStartupSplash();
                HandleFatalException(ex, "应用启动异常");
            }
        }

        private IHost CreateAndStartHost()
        {
            var host = Host.CreateDefaultBuilder()
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
                    services.AddSingleton<IResiliencePolicyService, ResiliencePolicyService>();
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<IProfileService, ProfileService>();
                    services.AddSingleton<IVersionService, VersionService>();
                    services.AddSingleton<IComfyPathService, ComfyPathService>();
                    services.AddSingleton<IPythonPathService, PythonPathService>();
                    services.AddSingleton<ICudaDeviceDiscoveryService, CudaDeviceDiscoveryService>();
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

            host.Start();
            return host;
        }

        private void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogGlobalException(ex, "未处理异常(AppDomain)", showDialog: true);
            };

            DispatcherUnhandledException += (_, args) =>
            {
                args.Handled = true;
                LogGlobalException(args.Exception, "未处理异常(UI线程)", showDialog: true);
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
                LogGlobalException(args.Exception, "未观察到的任务异常", showDialog: false);
            };
        }

        private void LogGlobalException(Exception? exception, string source, bool showDialog)
        {
            try
            {
                var details = BuildExceptionDetails(exception, source);
                Console.Error.WriteLine(details);
                Debug.WriteLine(details);

                if (GlobalExceptionPolicy.IsRecoverableNetworkException(exception))
                {
                    var message = $"{source}: {exception?.GetType().Name ?? "Exception"} - {exception?.Message ?? "(null)"}";
                    if (_logService != null)
                    {
                        _logService.Log(message, GUILogLevel.Warning);
                    }
                    else
                    {
                        FileLogWriter.WriteRaw(details);
                    }
                }
                else
                {
                    if (_logService != null)
                    {
                        _logService.LogError(source, exception);
                    }
                    else
                    {
                        FileLogWriter.WriteRaw(details);
                    }
                }

                if (!showDialog || GlobalExceptionPolicy.IsRecoverableNetworkException(exception))
                {
                    return;
                }

                if (Interlocked.Exchange(ref _fatalExceptionHandled, 1) == 1)
                {
                    return;
                }

                ShowGlobalExceptionDialog(details, shutdownAfterClose: false);
            }
            catch
            {
                // 日志失败不阻塞异常收口
            }
        }

        private static void TryAttachParentConsole()
        {
            try
            {
                AttachConsole(AttachConsoleParentProcess);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"附加父控制台失败: {ex}");
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        private void HandleFatalException(Exception? exception, string source)
        {
            if (Interlocked.Exchange(ref _fatalExceptionHandled, 1) == 1)
            {
                return;
            }

            try
            {
                var details = BuildExceptionDetails(exception, source);
                Console.Error.WriteLine(details);
                Debug.WriteLine(details);

                if (_logService != null)
                {
                    _logService.LogError(source, exception);
                }
                else
                {
                    FileLogWriter.WriteRaw(details);
                }

                ShowGlobalExceptionDialog(details, shutdownAfterClose: true);
            }
            catch
            {
                // 记录失败不应影响异常收口。
            }
        }

        private void ShowGlobalExceptionDialog(string details, bool shutdownAfterClose)
        {
            try
            {
                void ShowDialogCore()
                {
                    var dialog = new GlobalExceptionDialog(details);
                    if (MainWindow is Window mainWindow)
                    {
                        dialog.Owner = mainWindow;
                    }

                    dialog.ShowDialog();

                    if (shutdownAfterClose && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    {
                        Shutdown();
                    }
                }

                if (Dispatcher.CheckAccess())
                {
                    ShowDialogCore();
                    return;
                }

                Dispatcher.Invoke(ShowDialogCore);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示全局异常弹窗失败: {ex}");
                try
                {
                    MessageBox.Show(details, "程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception secondaryEx)
                {
                    Debug.WriteLine($"显示全局异常兜底消息框失败: {secondaryEx}");
                }
            }
        }

        private static void InitializeFileLogging()
        {
            var logDirectory = FileLogWriter.Initialize(AppContext.BaseDirectory, retentionDays: 14);
            if (!string.IsNullOrWhiteSpace(logDirectory))
            {
                FileLogWriter.Log(nameof(App), $"文件日志已初始化，目录: {logDirectory}", GUILogLevel.Success);
            }
            else
            {
                Debug.WriteLine("文件日志初始化失败，后续仅保留 Console/Debug 输出。");
            }
        }

        private static void LogStartupContext(string source)
        {
            FileLogWriter.Log(source, $"程序集: {typeof(App).Assembly.GetName().Name}", GUILogLevel.Info);
            FileLogWriter.Log(source, $"版本: {typeof(App).Assembly.GetName().Version}", GUILogLevel.Info);
            FileLogWriter.Log(source, $"进程 ID: {Environment.ProcessId}", GUILogLevel.Info);
            FileLogWriter.Log(source, $"进程路径: {Environment.ProcessPath ?? "(null)"}", GUILogLevel.Info);
            FileLogWriter.Log(source, $"命令行: {Environment.CommandLine}", GUILogLevel.Info);
            FileLogWriter.Log(source, $"当前目录: {Environment.CurrentDirectory}", GUILogLevel.Info);
            FileLogWriter.Log(source, $"基础目录: {AppContext.BaseDirectory}", GUILogLevel.Info);
            FileLogWriter.Log(source, $"OS: {Environment.OSVersion}", GUILogLevel.Info);
            FileLogWriter.Log(source, $".NET: {Environment.Version}", GUILogLevel.Info);
        }

        private static void LogStartupSettings(AppSettings startupSettings)
        {
            FileLogWriter.Log(nameof(App), $"AppName: {startupSettings.AppName}", GUILogLevel.Info);
            FileLogWriter.Log(nameof(App), $"DataRoot: {startupSettings.DataRoot}", GUILogLevel.Info);
            FileLogWriter.Log(nameof(App), $"PythonRoot: {startupSettings.PythonRoot}", GUILogLevel.Info);
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
