using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

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
    private readonly IHardwareMonitorService _hardwareMonitorService;
    private readonly IEnvironmentCheckService _environmentCheckService;
    private readonly DispatcherTimer _gpuStatusTimer;

    private string _activeProfileId = DefaultProfileId;
    private ComfyConfiguration? _currentConfiguration;
    private bool _startupCleanupCompleted;
    private bool _startupExtraModelYamlSynced;
    private bool _startupBannerLogged;

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

    [ObservableProperty]
    private string _statusBarText = "状态: 未启动";

    [ObservableProperty]
    private string _comfyUiSystemStatsText = "暂无 ComfyUI system_stats 数据";

    [ObservableProperty]
    private GpuRingStatusItem _cpuStatusItem = new();

    public ObservableCollection<SystemStatusItem> ComfyUiSystemRows { get; } = new();
    public ObservableCollection<SystemDeviceItem> ComfyUiDeviceRows { get; } = new();
    public ObservableCollection<GpuRingStatusItem> GpuStatusItems { get; } = new();

    public IAsyncRelayCommand ToggleProcessCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand OpenWebPageCommand { get; }
    public string WebUiBaseUrl => $"http://127.0.0.1:{_currentConfiguration?.Network.Port ?? 8188}";
    public string OpenWebPageToolTip => $"在浏览器打开 {WebUiBaseUrl} (ComfyUI WebUI BaseUrl)";

    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        ConfigurationViewModel configurationViewModel,
        VersionManagerViewModel versionManagerViewModel,
        ProfileManagerViewModel profileManagerViewModel,
        ProcessMonitorViewModel processMonitorViewModel,
        HardwareMonitorViewModel hardwareMonitorViewModel,
        SettingsViewModel settingsViewModel,
        ResourcesViewModel resourcesViewModel,
        IProcessService processService,
        IComfyPathService comfyPathService,
        IProfileService profileService,
        IConfigurationService configurationService,
        ISettingsService settingsService,
        IHardwareMonitorService hardwareMonitorService,
        IEnvironmentCheckService environmentCheckService,
        ILogService logService)
    {
        _processService = processService;
        _comfyPathService = comfyPathService;
        _profileService = profileService;
        _configurationService = configurationService;
        _settingsService = settingsService;
        _hardwareMonitorService = hardwareMonitorService;
        _environmentCheckService = environmentCheckService;
        _logService = logService;

        _viewModels = new Dictionary<string, ViewModelBase>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = dashboardViewModel,
            ["Configuration"] = configurationViewModel,
            ["VersionManager"] = versionManagerViewModel,
            ["ProfileManager"] = profileManagerViewModel,
            ["ProcessMonitor"] = processMonitorViewModel,
            ["HardwareMonitor"] = hardwareMonitorViewModel,
            ["Settings"] = settingsViewModel,
            ["Resources"] = resourcesViewModel
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
        _processService.HeartbeatStatusChanged += OnHeartbeatStatusChanged;
        _processService.SystemStatsUpdated += OnSystemStatsUpdated;
        _logService.LogReceived += OnLogReceived;

        WeakReferenceMessenger.Default.Register<AppSettingsChangedMessage>(this, (_, message) =>
        {
            RunOnUiThread(() => UpdateAppTitle(message.Value));
        });

        _gpuStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _gpuStatusTimer.Tick += (_, _) => RefreshGpuStatusSummary();
        _gpuStatusTimer.Start();

        _ = LoadAppTitleAsync();
        RefreshGpuStatusSummary();

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

            if (!_startupBannerLogged)
            {
                _startupBannerLogged = true;
                _ = _environmentCheckService.LogStartupBannerAsync(
                    null,
                    _comfyPathService.ComfyRootPath);
            }

            var profiles = await _profileService.GetProfilesAsync();
            var defaultProfile = profiles.FirstOrDefault(profile => profile.IsDefault);
            _activeProfileId = defaultProfile?.Id ?? DefaultProfileId;
            _currentConfiguration = await _configurationService.LoadConfigurationAsync(_activeProfileId);
            _processService.ConfigureApiEndpoint(_currentConfiguration.Network.Listen, _currentConfiguration.Network.Port);
            OnPropertyChanged(nameof(WebUiBaseUrl));
            OnPropertyChanged(nameof(OpenWebPageToolTip));

            if (!_startupCleanupCompleted && _comfyPathService.IsValid && !string.IsNullOrWhiteSpace(_comfyPathService.ComfyRootPath))
            {
                var killed = await _processService.CleanupLingeringProcessesAsync(_comfyPathService.ComfyRootPath);
                _startupCleanupCompleted = true;
                if (killed > 0)
                {
                    StatusMessage = $"启动前已清理 {killed} 个残留 ComfyUI 进程";
                    _ = Task.Delay(3000).ContinueWith(_ => RunOnUiThread(() => StatusMessage = string.Empty));
                }
            }

            if (!_startupExtraModelYamlSynced && _comfyPathService.IsValid)
            {
                await SyncExtraModelPathsYamlOnStartupAsync();
                _startupExtraModelYamlSynced = true;
            }

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

    private void OnHeartbeatStatusChanged(object? sender, bool isAlive)
    {
        RunOnUiThread(() => StatusBarText = isAlive ? "状态: 就绪" : "状态: 未启动");
    }

    private void OnSystemStatsUpdated(object? sender, string statsJson)
    {
        if (string.IsNullOrWhiteSpace(statsJson))
        {
            return;
        }

        RunOnUiThread(() => ParseSystemStats(statsJson));
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
        StatusBarText = status.IsRunning ? "状态: 就绪" : "状态: 未启动";
        OpenWebPageCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAppTitleAsync()
    {
        var settings = await _settingsService.LoadAsync();
        RunOnUiThread(() => UpdateAppTitle(settings));
    }

    [ObservableProperty]
    private string _appName = "LAUNCHER";

    [ObservableProperty]
    private string _appVersionText = "v1.0.0";

    private void UpdateAppTitle(AppSettings settings)
    {
        var title = string.IsNullOrWhiteSpace(settings.AppName) ? "ComfyShell" : settings.AppName;
        AppName = string.IsNullOrWhiteSpace(settings.AppName) ? "LAUNCHER" : settings.AppName;
        
        var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var version = assemblyVersion == null
            ? "1.0.0"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}";
        AppVersionText = $"v{version}";
        AppTitle = $"{title}  {VersionName} v{version}";
    }

    private void OpenWebPage()
    {
        Process.Start(new ProcessStartInfo(WebUiBaseUrl) { UseShellExecute = true });
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action);
    }

    private async Task SyncExtraModelPathsYamlOnStartupAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_comfyPathService.ComfyUiPath))
            {
                return;
            }

            var yamlPath = Path.Combine(_comfyPathService.ComfyUiPath, "extra_model_paths.yaml");
            var extraBaseDir = _currentConfiguration?.Paths?.ExtraModelBaseDirectory;

            if (string.IsNullOrWhiteSpace(extraBaseDir))
            {
                if (File.Exists(yamlPath))
                {
                    File.Delete(yamlPath);
                }
                return;
            }

            if (!Directory.Exists(extraBaseDir))
            {
                _logService.Log($"扩展模型目录不存在，跳过自动同步: {extraBaseDir}");
                return;
            }

            var yamlContent = ExtraModelPathsYamlHelper.GenerateYamlContent(extraBaseDir);
            if (File.Exists(yamlPath))
            {
                var existing = await File.ReadAllTextAsync(yamlPath);
                if (string.Equals(existing, yamlContent, StringComparison.Ordinal))
                {
                    return;
                }
            }

            await File.WriteAllTextAsync(yamlPath, yamlContent, System.Text.Encoding.UTF8);
            _logService.Log("启动时已自动同步 extra_model_paths.yaml");
        }
        catch (Exception ex)
        {
            _logService.LogError("启动时同步 extra_model_paths.yaml 失败", ex);
        }
    }

    private void ParseSystemStats(string statsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(statsJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            ComfyUiSystemRows.Clear();
            ComfyUiDeviceRows.Clear();

            if (root.TryGetProperty("system", out var system) && system.ValueKind == JsonValueKind.Object)
            {
                AddSystemRow("OS", GetString(system, "os"));
                AddSystemRow("ComfyUI", GetString(system, "comfyui_version"));
                AddSystemRow("Frontend", GetString(system, "required_frontend_version"));
                AddSystemRow("Templates(Installed)", GetString(system, "installed_templates_version"));
                AddSystemRow("Templates(Required)", GetString(system, "required_templates_version"));
                AddSystemRow("Python", GetString(system, "python_version"));
                AddSystemRow("PyTorch", GetString(system, "pytorch_version"));
                AddSystemRow("Embedded Python", GetBool(system, "embedded_python"));

                var ramTotal = GetLong(system, "ram_total");
                var ramFree = GetLong(system, "ram_free");
                if (ramTotal.HasValue)
                {
                    AddSystemRow("RAM Total", FormatBytes(ramTotal.Value));
                }
                if (ramFree.HasValue)
                {
                    AddSystemRow("RAM Free", FormatBytes(ramFree.Value));
                }
            }

            if (root.TryGetProperty("devices", out var devices) && devices.ValueKind == JsonValueKind.Array)
            {
                foreach (var device in devices.EnumerateArray())
                {
                    if (device.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    ComfyUiDeviceRows.Add(new SystemDeviceItem
                    {
                        Name = GetString(device, "name") ?? "-",
                        Type = GetString(device, "type") ?? "-",
                        Index = GetInt(device, "index")?.ToString() ?? "-",
                        VramTotal = FormatBytes(GetLong(device, "vram_total") ?? 0),
                        VramFree = FormatBytes(GetLong(device, "vram_free") ?? 0)
                    });
                }
            }
        }
        catch (JsonException)
        {
            ComfyUiSystemRows.Clear();
            ComfyUiDeviceRows.Clear();
            ComfyUiSystemRows.Add(new SystemStatusItem { Key = "Raw", Value = statsJson });
        }
    }

    private void RefreshGpuStatusSummary()
    {
        try
        {
            var snapshot = _hardwareMonitorService.GetSnapshot();
            var cpuPercent = snapshot.CpuLoadPercent.HasValue ? Math.Clamp(snapshot.CpuLoadPercent.Value, 0, 100) : 0;
            var cpuMemoryPercent = ResolveMemoryPercent(snapshot.MemoryUsedMb, snapshot.MemoryTotalMb);

            CpuStatusItem.DisplayName = "CPU";
            CpuStatusItem.DetailedName = string.IsNullOrWhiteSpace(snapshot.CpuName) ? "CPU" : snapshot.CpuName;
            CpuStatusItem.LoadPercent = cpuPercent;
            CpuStatusItem.LoadArcData = GpuRingStatusItem.BuildArcPathData(cpuPercent);
            CpuStatusItem.LoadTooltipValue = $"{cpuPercent:F0}%";
            CpuStatusItem.MemoryPercent = cpuMemoryPercent;
            CpuStatusItem.MemoryArcData = GpuRingStatusItem.BuildArcPathData(cpuMemoryPercent);
            CpuStatusItem.MemoryTooltipValue = FormatMemoryPair(snapshot.MemoryUsedMb, snapshot.MemoryTotalMb);
            CpuStatusItem.LoadRingColor = "#F6A23A";
            CpuStatusItem.MemoryRingColor = "#C084FC";

            var gpuSnapshots = snapshot.Gpus;
            if (gpuSnapshots.Count == 0)
            {
                GpuStatusItems.Clear();
                return;
            }

            if (GpuStatusItems.Count != gpuSnapshots.Count)
            {
                GpuStatusItems.Clear();
                for (var i = 0; i < gpuSnapshots.Count; i++)
                {
                    var gpu = gpuSnapshots[i];
                    var percent = ResolveGpuUsagePercent(gpu);
                    GpuStatusItems.Add(new GpuRingStatusItem
                    {
                        DisplayName = $"GPU{i}",
                        DetailedName = string.IsNullOrWhiteSpace(gpu.Name) ? $"GPU{i}" : gpu.Name,
                        LoadPercent = percent,
                        LoadArcData = GpuRingStatusItem.BuildArcPathData(percent),
                        LoadTooltipValue = $"{percent:F0}%",
                        MemoryPercent = ResolveMemoryPercent(gpu.MemoryUsedMb, gpu.MemoryTotalMb),
                        MemoryArcData = GpuRingStatusItem.BuildArcPathData(ResolveMemoryPercent(gpu.MemoryUsedMb, gpu.MemoryTotalMb)),
                        MemoryTooltipValue = FormatMemoryPair(gpu.MemoryUsedMb, gpu.MemoryTotalMb),
                        LoadRingColor = "#6EC1FF",
                        MemoryRingColor = "#4ADE80"
                    });
                }

                return;
            }

            for (var i = 0; i < gpuSnapshots.Count; i++)
            {
                var gpu = gpuSnapshots[i];
                var percent = ResolveGpuUsagePercent(gpu);
                var item = GpuStatusItems[i];
                item.DisplayName = $"GPU{i}";
                item.DetailedName = string.IsNullOrWhiteSpace(gpu.Name) ? $"GPU{i}" : gpu.Name;
                item.LoadPercent = percent;
                item.LoadArcData = GpuRingStatusItem.BuildArcPathData(percent);
                item.LoadTooltipValue = $"{percent:F0}%";
                var memoryPercent = ResolveMemoryPercent(gpu.MemoryUsedMb, gpu.MemoryTotalMb);
                item.MemoryPercent = memoryPercent;
                item.MemoryArcData = GpuRingStatusItem.BuildArcPathData(memoryPercent);
                item.MemoryTooltipValue = FormatMemoryPair(gpu.MemoryUsedMb, gpu.MemoryTotalMb);
                item.LoadRingColor = "#6EC1FF";
                item.MemoryRingColor = "#4ADE80";
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("刷新顶部显卡状态失败", ex);
            CpuStatusItem.DisplayName = string.Empty;
            CpuStatusItem.DetailedName = string.Empty;
            CpuStatusItem.LoadPercent = 0;
            CpuStatusItem.LoadArcData = string.Empty;
            CpuStatusItem.LoadTooltipValue = "--";
            CpuStatusItem.MemoryPercent = 0;
            CpuStatusItem.MemoryArcData = string.Empty;
            CpuStatusItem.MemoryTooltipValue = "--";
            GpuStatusItems.Clear();
        }
    }

    private static double ResolveGpuUsagePercent(GpuInfoSnapshot gpu)
    {
        if (gpu.LoadPercent.HasValue)
        {
            return Math.Clamp(gpu.LoadPercent.Value, 0, 100);
        }

        if (gpu.MemoryUsedMb.HasValue && gpu.MemoryTotalMb.HasValue && gpu.MemoryTotalMb.Value > 0)
        {
            return Math.Clamp(gpu.MemoryUsedMb.Value * 100.0 / gpu.MemoryTotalMb.Value, 0, 100);
        }

        return 0;
    }

    private static double ResolveMemoryPercent(double? usedMb, double? totalMb)
    {
        if (!usedMb.HasValue || !totalMb.HasValue || totalMb.Value <= 0)
        {
            return 0;
        }

        return Math.Clamp(usedMb.Value * 100.0 / totalMb.Value, 0, 100);
    }

    private static string FormatMemoryPair(double? usedMb, double? totalMb)
    {
        if (!usedMb.HasValue || !totalMb.HasValue || totalMb.Value <= 0)
        {
            return "--";
        }

        return $"{FormatGbCompact(usedMb.Value / 1024.0)}/{FormatGbCompact(totalMb.Value / 1024.0)}";
    }

    private static string FormatGbCompact(double gb)
    {
        var rounded = Math.Round(gb, 1);
        if (Math.Abs(rounded - Math.Round(rounded)) < 0.0001)
        {
            return $"{Math.Round(rounded):F0}G";
        }

        return $"{rounded:F1}G";
    }

    private void AddSystemRow(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ComfyUiSystemRows.Add(new SystemStatusItem { Key = key, Value = value });
    }

    private static string? GetString(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out var value) ? value.ToString() : null;
    }

    private static string? GetBool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.True ? "Yes" :
               value.ValueKind == JsonValueKind.False ? "No" : value.ToString();
    }

    private static long? GetLong(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out var value) && value.TryGetInt64(out var longValue) ? longValue : null;
    }

    private static int? GetInt(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out var value) && value.TryGetInt32(out var intValue) ? intValue : null;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var gb = bytes / (1024.0 * 1024.0 * 1024.0);
        if (gb >= 1)
        {
            return $"{gb:F2} GB";
        }

        var mb = bytes / (1024.0 * 1024.0);
        if (mb >= 1)
        {
            return $"{mb:F2} MB";
        }

        var kb = bytes / 1024.0;
        return kb >= 1 ? $"{kb:F2} KB" : $"{bytes} B";
    }
}

public class SystemStatusItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class SystemDeviceItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Index { get; set; } = string.Empty;
    public string VramTotal { get; set; } = string.Empty;
    public string VramFree { get; set; } = string.Empty;
}

public partial class GpuRingStatusItem : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _detailedName = string.Empty;

    [ObservableProperty]
    private double _loadPercent;

    [ObservableProperty]
    private string _loadArcData = string.Empty;

    [ObservableProperty]
    private double _memoryPercent;

    [ObservableProperty]
    private string _memoryArcData = string.Empty;

    [ObservableProperty]
    private string _loadRingColor = "#6EC1FF";

    [ObservableProperty]
    private string _memoryRingColor = "#4ADE80";

    [ObservableProperty]
    private string _loadTooltipValue = "0%";

    [ObservableProperty]
    private string _memoryTooltipValue = "--";

    public string LoadPercentText => $"{LoadPercent:F0}%";
    public string MemoryPercentText => $"{MemoryPercent:F0}%";

    partial void OnLoadPercentChanged(double value)
    {
        OnPropertyChanged(nameof(LoadPercentText));
    }

    partial void OnMemoryPercentChanged(double value)
    {
        OnPropertyChanged(nameof(MemoryPercentText));
    }

    public static string BuildArcPathData(double percent)
    {
        if (percent <= 0)
        {
            return string.Empty;
        }

        if (percent >= 99.9)
        {
            return "M 20,5 A 15,15 0 1 1 20,35 A 15,15 0 1 1 20,5";
        }

        var angle = percent / 100.0 * 360.0;
        var radians = angle * Math.PI / 180.0;
        var endX = 20 + 15 * Math.Sin(radians);
        var endY = 20 - 15 * Math.Cos(radians);
        var largeArcFlag = angle > 180 ? 1 : 0;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "M 20,5 A 15,15 0 {0} 1 {1:F2},{2:F2}", largeArcFlag, endX, endY);
    }
}
