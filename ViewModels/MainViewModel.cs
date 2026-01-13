using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;
using System.Diagnostics;
using System.Linq;

namespace WpfDesktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const string DefaultProfileId = "default";
    public const string VersionName = "ComfyDesktop by Proly";
    private readonly IReadOnlyDictionary<string, ViewModelBase> _viewModels;
    private readonly IProcessService _processService;
    private readonly IComfyPathService _comfyPathService;
    private readonly IProfileService _profileService;
    private readonly IConfigurationService _configurationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;

    private string _activeProfileId = DefaultProfileId;
    private ComfyConfiguration? _currentConfiguration;

    [ObservableProperty]
    private string _appTitle = "ComfyShell";

    [ObservableProperty]
    private string _processStatusText = "未运行";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _lastLogLine = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private DateTime? _lastUpdated;

    public IAsyncRelayCommand ToggleProcessCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand OpenWebPageCommand { get; }

    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        ConfigurationViewModel configurationViewModel,
        VersionManagerViewModel versionManagerViewModel,
        ProfileManagerViewModel profileManagerViewModel,
        ProcessMonitorViewModel processMonitorViewModel,
        HardwareMonitorViewModel hardwareMonitorViewModel,
        SettingsViewModel settingsViewModel,
        IProcessService processService,
        IComfyPathService comfyPathService,
        IProfileService profileService,
        IConfigurationService configurationService,
        ISettingsService settingsService,
        ILogService logService)
    {
        _processService = processService;
        _comfyPathService = comfyPathService;
        _profileService = profileService;
        _configurationService = configurationService;
        _settingsService = settingsService;
        _logService = logService;

        _viewModels = new Dictionary<string, ViewModelBase>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = dashboardViewModel,
            ["Configuration"] = configurationViewModel,
            ["VersionManager"] = versionManagerViewModel,
            ["ProfileManager"] = profileManagerViewModel,
            ["ProcessMonitor"] = processMonitorViewModel,
            ["HardwareMonitor"] = hardwareMonitorViewModel,
            ["Settings"] = settingsViewModel
        };

        var initialView = _viewModels["Dashboard"];
        CurrentView = initialView;
        if (initialView is INavigationAware navigationAware)
        {
            _ = navigationAware.OnNavigatedToAsync();
        }

        NavigateCommand = new RelayCommand<string>(NavigateTo);

        // 初始化命令
        ToggleProcessCommand = new AsyncRelayCommand(ToggleProcessAsync, () => !IsLoading);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenWebPageCommand = new RelayCommand(OpenWebPage, () => IsRunning);

        _processService.StatusChanged += OnStatusChanged;
        _processService.OutputReceived += OnOutputReceived;
        _logService.LogReceived += OnLogReceived;

        WeakReferenceMessenger.Default.Register<AppSettingsChangedMessage>(this, (_, message) =>
        {
            RunOnUiThread(() => UpdateAppTitle(message.Value));
        });

        _ = LoadAppTitleAsync();

        // 初始化状态
        _ = RefreshAsync();
    }

    private object? _currentView;

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public IRelayCommand<string> NavigateCommand { get; }

    private async void NavigateTo(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (_viewModels.TryGetValue(key, out var viewModel))
        {
            CurrentView = viewModel;

            if (viewModel is INavigationAware navigationAware)
            {
                await navigationAware.OnNavigatedToAsync();
            }
        }
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            _comfyPathService.Refresh();

            var profiles = await _profileService.GetProfilesAsync();
            var defaultProfile = profiles.FirstOrDefault(profile => profile.IsDefault);
            _activeProfileId = defaultProfile?.Id ?? DefaultProfileId;
            _currentConfiguration = await _configurationService.LoadConfigurationAsync(_activeProfileId);

            var status = await _processService.GetStatusAsync();
            UpdateStatus(status);

            LastUpdated = DateTime.Now;
        }
        finally
        {
            IsLoading = false;
            RefreshCommand.NotifyCanExecuteChanged();
            ToggleProcessCommand.NotifyCanExecuteChanged();
            OpenWebPageCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ToggleProcessAsync()
    {
        if (IsRunning)
        {
            await StopAsync();
        }
        else
        {
            await QuickStartAsync();
        }
    }

    private async Task QuickStartAsync()
    {
        IsLoading = true;
        try
        {
            _comfyPathService.Refresh();

            if (!_comfyPathService.IsValid)
            {
                StatusMessage = "未找到 ComfyUI，请检查目录结构";
                await Task.Delay(2000);
                StatusMessage = string.Empty;
                return;
            }

            var comfyRootPath = _comfyPathService.ComfyRootPath!;
            var configuration = await _configurationService.LoadConfigurationAsync(_activeProfileId);
            var started = await _processService.StartAsync(comfyRootPath, configuration);
            StatusMessage = started ? "已发送启动命令" : "启动失败";

            // 3秒后清除临时消息
            _ = Task.Delay(3000).ContinueWith(_ => RunOnUiThread(() => StatusMessage = string.Empty));

            await RefreshAsync();
        }
        finally
        {
            IsLoading = false;
            RefreshCommand.NotifyCanExecuteChanged();
            ToggleProcessCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task StopAsync()
    {
        IsLoading = true;
        try
        {
            var stopped = await _processService.StopAsync();
            StatusMessage = stopped ? "已发送停止命令" : "停止失败或未运行";

            // 3秒后清除临时消息
            _ = Task.Delay(3000).ContinueWith(_ => RunOnUiThread(() => StatusMessage = string.Empty));

            await RefreshAsync();
        }
        finally
        {
            IsLoading = false;
            RefreshCommand.NotifyCanExecuteChanged();
            ToggleProcessCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnStatusChanged(object? sender, ProcessStatus status)
    {
        RunOnUiThread(() => UpdateStatus(status));
    }

    private void OnOutputReceived(object? sender, string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            RunOnUiThread(() => LastLogLine = line.Trim());
        }
    }

    private void OnLogReceived(object? sender, string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            RunOnUiThread(() => LastLogLine = line.Trim());
        }
    }

    private void UpdateStatus(ProcessStatus? status)
    {
        if (status == null)
        {
            ProcessStatusText = "未运行";
            IsRunning = false;
            return;
        }

        ProcessStatusText = status.State switch
        {
            ProcessState.Starting => "启动中",
            ProcessState.Running => "运行中",
            ProcessState.Stopping => "停止中",
            ProcessState.Stopped => "已停止",
            ProcessState.Error => "异常",
            _ => "空闲"
        };
        IsRunning = status.IsRunning;
        OpenWebPageCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAppTitleAsync()
    {
        var settings = await _settingsService.LoadAsync();
        RunOnUiThread(() => UpdateAppTitle(settings));
    }

    private void UpdateAppTitle(AppSettings settings)
    {
        var title = string.IsNullOrWhiteSpace(settings.AppName) ? "ComfyShell" : settings.AppName;
        AppTitle = $"{title}  {VersionName}";
    }

    private void OpenWebPage()
    {
        var listen = _currentConfiguration?.Network.Listen ?? "127.0.0.1";
        var port = _currentConfiguration?.Network.Port ?? 8188;
        var url = $"http://{listen}:{port}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void RunOnUiThread(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            action();
            return;
        }

        System.Windows.Application.Current?.Dispatcher?.Invoke(action);
    }
}
