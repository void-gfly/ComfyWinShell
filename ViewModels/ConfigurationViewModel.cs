using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WpfDesktop.Models;
using WpfDesktop.Models.Enums;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class ConfigurationViewModel : ViewModelBase, INavigationAware
{
    private const string DefaultProfileId = "default";
    private const string ManagerGitUrl = "https://github.com/Comfy-Org/ComfyUI-Manager.git";
    private const string ManagerDirName = "ComfyUI-Manager";

    private readonly IConfigurationService _configurationService;
    private readonly IComfyPathService _comfyPathService;
    private readonly IProfileService _profileService;
    private readonly IHardwareMonitorService _hardwareMonitorService;
    private readonly ArgumentBuilder _argumentBuilder;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;
    private readonly List<INotifyPropertyChanged> _trackedNotifiers = new();
    private readonly List<INotifyCollectionChanged> _trackedCollections = new();
    private bool _isSyncingText;
    private bool _isInstallingManager;
    private string _currentProfileId = DefaultProfileId;

    public ConfigurationViewModel(
        IConfigurationService configurationService,
        IComfyPathService comfyPathService,
        IProfileService profileService,
        IHardwareMonitorService hardwareMonitorService,
        ArgumentBuilder argumentBuilder,
        IDialogService dialogService,
        ILogService logService)
    {
        _configurationService = configurationService;
        _comfyPathService = comfyPathService;
        _profileService = profileService;
        _hardwareMonitorService = hardwareMonitorService;
        _argumentBuilder = argumentBuilder;
        _dialogService = dialogService;
        _logService = logService;

        LoadDefaultCommand = new AsyncRelayCommand(LoadDefaultAsync);
        SaveDefaultCommand = new AsyncRelayCommand(SaveDefaultAsync, () => !IsLoading);
        BrowseDirectoryCommand = new RelayCommand<string>(BrowseDirectory);

        Configuration = new ComfyConfiguration();
        AttachConfigurationHandlers(Configuration);

        RefreshCudaDevices();
        _ = LoadDefaultAsync();
    }

    public async Task OnNavigatedToAsync()
    {
        await LoadDefaultAsync();
    }

    [ObservableProperty]
    private ComfyConfiguration _configuration;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _extraModelPathsText = string.Empty;

    [ObservableProperty]
    private string _fastOptionsText = string.Empty;

    [ObservableProperty]
    private string _whitelistCustomNodesText = string.Empty;

    public IAsyncRelayCommand LoadDefaultCommand { get; }

    public IAsyncRelayCommand SaveDefaultCommand { get; }

    public IRelayCommand<string> BrowseDirectoryCommand { get; }

    public IReadOnlyList<VramMode> VramModes { get; } = Enum.GetValues<VramMode>();

    public IReadOnlyList<ForcePrecisionMode> ForcePrecisionModes { get; } = Enum.GetValues<ForcePrecisionMode>();

    public IReadOnlyList<UnetPrecisionMode> UnetPrecisionModes { get; } = Enum.GetValues<UnetPrecisionMode>();

    public IReadOnlyList<VaePrecisionMode> VaePrecisionModes { get; } = Enum.GetValues<VaePrecisionMode>();

    public IReadOnlyList<TextEncoderPrecisionMode> TextEncoderPrecisionModes { get; } = Enum.GetValues<TextEncoderPrecisionMode>();

    public IReadOnlyList<AttentionMode> AttentionModes { get; } = Enum.GetValues<AttentionMode>();

    public IReadOnlyList<UpcastMode> UpcastModes { get; } = Enum.GetValues<UpcastMode>();

    public IReadOnlyList<PreviewMethod> PreviewMethods { get; } = Enum.GetValues<PreviewMethod>();

    public IReadOnlyList<CacheMode> CacheModes { get; } = Enum.GetValues<CacheMode>();

    public IReadOnlyList<LogLevel> LogLevels { get; } = Enum.GetValues<LogLevel>();

    /// <summary>
    /// 可选的 CUDA 设备列表
    /// </summary>
    public ObservableCollection<CudaDeviceOption> CudaDevices { get; } = new();

    /// <summary>
    /// 当前选中的 CUDA 设备
    /// </summary>
    [ObservableProperty]
    private CudaDeviceOption? _selectedCudaDevice;

    partial void OnSelectedCudaDeviceChanged(CudaDeviceOption? value)
    {
        if (Configuration?.Device != null)
        {
            Configuration.Device.CudaDevice = value?.DeviceId;
        }
    }

    private async Task LoadDefaultAsync()
    {
        IsLoading = true;
        try
        {
            var profiles = await _profileService.GetProfilesAsync();
            var defaultProfile = profiles.FirstOrDefault(p => p.IsDefault);
            _currentProfileId = defaultProfile?.Id ?? DefaultProfileId;

            Configuration = await _configurationService.LoadConfigurationAsync(_currentProfileId);
            AttachConfigurationHandlers(Configuration);

            // 同步选中的 CUDA 设备
            SyncSelectedCudaDevice();
        }
        finally
        {
            IsLoading = false;
            SaveDefaultCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task SaveDefaultAsync()
    {
        IsLoading = true;
        try
        {
            await _configurationService.SaveConfigurationAsync(_currentProfileId, Configuration);
            
            // 如果设置了扩展模型目录，自动生成 extra_model_paths.yaml
            await GenerateExtraModelPathsYamlAsync();
        }
        finally
        {
            IsLoading = false;
            SaveDefaultCommand.NotifyCanExecuteChanged();
        }
    }

    private void BrowseDirectory(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var initialDirectory = target switch
        {
            "Base" => Configuration.Paths.BaseDirectory,
            "ExtraModelBase" => Configuration.Paths.ExtraModelBaseDirectory,
            "Output" => Configuration.Paths.OutputDirectory,
            "Input" => Configuration.Paths.InputDirectory,
            "Temp" => Configuration.Paths.TempDirectory,
            "User" => Configuration.Paths.UserDirectory,
            _ => null
        };

        var selected = _dialogService.SelectFolder("选择目录", initialDirectory);
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        switch (target)
        {
            case "Base":
                Configuration.Paths.BaseDirectory = selected;
                break;
            case "ExtraModelBase":
                Configuration.Paths.ExtraModelBaseDirectory = selected;
                break;
            case "Output":
                Configuration.Paths.OutputDirectory = selected;
                break;
            case "Input":
                Configuration.Paths.InputDirectory = selected;
                break;
            case "Temp":
                Configuration.Paths.TempDirectory = selected;
                break;
            case "User":
                Configuration.Paths.UserDirectory = selected;
                break;
        }
    }

    private void AttachConfigurationHandlers(ComfyConfiguration configuration)
    {
        DetachConfigurationHandlers();

        Track(configuration);
        Track(configuration.Network);
        Track(configuration.Paths);
        Track(configuration.Device);
        Track(configuration.Memory);
        Track(configuration.Precision);
        Track(configuration.Attention);
        Track(configuration.Preview);
        Track(configuration.Cache);
        Track(configuration.Manager);
        Track(configuration.Launch);
        Track(configuration.Miscellaneous);

        TrackCollection(configuration.Paths.ExtraModelPathsConfig);
        TrackCollection(configuration.Miscellaneous.FastOptions);
        TrackCollection(configuration.Miscellaneous.WhitelistCustomNodes);

        SyncTextFromCollections();
    }

    private void Track(INotifyPropertyChanged notifier)
    {
        notifier.PropertyChanged += OnConfigurationPropertyChanged;
        _trackedNotifiers.Add(notifier);
    }

    private void TrackCollection(INotifyCollectionChanged collection)
    {
        collection.CollectionChanged += OnConfigurationCollectionChanged;
        _trackedCollections.Add(collection);
    }

    private void DetachConfigurationHandlers()
    {
        foreach (var notifier in _trackedNotifiers)
        {
            notifier.PropertyChanged -= OnConfigurationPropertyChanged;
        }

        foreach (var collection in _trackedCollections)
        {
            collection.CollectionChanged -= OnConfigurationCollectionChanged;
        }

        _trackedNotifiers.Clear();
        _trackedCollections.Clear();
    }

    private void OnConfigurationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTextFromCollections();
    }

    private void OnConfigurationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Configuration))
        {
            AttachConfigurationHandlers(Configuration);
        }

        // 当 EnableManager 被勾选时，检查并安装 ComfyUI-Manager
        if (ReferenceEquals(sender, Configuration?.Manager) && e.PropertyName == nameof(ManagerConfiguration.EnableManager))
        {
            if (Configuration?.Manager?.EnableManager == true)
            {
                _ = CheckAndInstallManagerAsync();
            }
        }
    }

    partial void OnExtraModelPathsTextChanged(string value)
    {
        UpdateCollectionFromText(Configuration.Paths.ExtraModelPathsConfig, value);
    }

    partial void OnFastOptionsTextChanged(string value)
    {
        UpdateCollectionFromText(Configuration.Miscellaneous.FastOptions, value);
    }

    partial void OnWhitelistCustomNodesTextChanged(string value)
    {
        UpdateCollectionFromText(Configuration.Miscellaneous.WhitelistCustomNodes, value);
    }

    private void SyncTextFromCollections()
    {
        if (_isSyncingText)
        {
            return;
        }

        _isSyncingText = true;
        ExtraModelPathsText = string.Join(Environment.NewLine, Configuration.Paths.ExtraModelPathsConfig);
        FastOptionsText = string.Join(Environment.NewLine, Configuration.Miscellaneous.FastOptions);
        WhitelistCustomNodesText = string.Join(Environment.NewLine, Configuration.Miscellaneous.WhitelistCustomNodes);
        _isSyncingText = false;
    }

    private void UpdateCollectionFromText(ICollection<string> collection, string text)
    {
        if (_isSyncingText)
        {
            return;
        }

        var items = text.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _isSyncingText = true;
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
        _isSyncingText = false;
    }

    /// <summary>
    /// 刷新 CUDA 设备列表
    /// </summary>
    private void RefreshCudaDevices()
    {
        CudaDevices.Clear();
        CudaDevices.Add(CudaDeviceOption.None);

        try
        {
            var snapshot = _hardwareMonitorService.GetSnapshot();
            // 只添加 NVIDIA GPU（CUDA 设备）
            var nvidiaGpus = snapshot.Gpus
                .Where(g => g.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                .ToList();

            for (var i = 0; i < nvidiaGpus.Count; i++)
            {
                CudaDevices.Add(CudaDeviceOption.Create(i, nvidiaGpus[i].Name));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("刷新 CUDA 设备列表失败", ex);
        }
    }

    /// <summary>
    /// 根据 Configuration.Device.CudaDevice 同步选中项
    /// </summary>
    private void SyncSelectedCudaDevice()
    {
        var cudaDeviceId = Configuration?.Device?.CudaDevice;
        if (cudaDeviceId.HasValue)
        {
            SelectedCudaDevice = CudaDevices.FirstOrDefault(d => d.DeviceId == cudaDeviceId.Value)
                                 ?? CudaDevices.FirstOrDefault(); // 找不到则选"未选择"
        }
        else
        {
            SelectedCudaDevice = CudaDevices.FirstOrDefault(); // 选"未选择"
        }
    }

    /// <summary>
    /// 检查 ComfyUI-Manager 是否已安装，如果没有则提示用户安装
    /// </summary>
    private async Task CheckAndInstallManagerAsync()
    {
        if (_isInstallingManager)
            return;

        if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
        {
            _dialogService.ShowError("未检测到有效的 ComfyUI 安装目录，无法安装 Manager。");
            Configuration.Manager.EnableManager = false;
            return;
        }

        var customNodesPath = Path.Combine(_comfyPathService.ComfyUiPath, "custom_nodes");
        var managerPath = Path.Combine(customNodesPath, ManagerDirName);

        // 检查 Manager 是否已安装
        if (Directory.Exists(managerPath))
            return;

        // 提示用户是否安装
        var confirm = _dialogService.Confirm(
            "检测到 ComfyUI-Manager 尚未安装。\n\n是否立即从 GitHub 克隆安装？\n\n" +
            $"仓库地址: {ManagerGitUrl}",
            "安装 ComfyUI-Manager");

        if (!confirm)
        {
            Configuration.Manager.EnableManager = false;
            return;
        }

        await InstallManagerAsync(customNodesPath, managerPath);
    }

    /// <summary>
    /// 通过 git clone 安装 ComfyUI-Manager
    /// </summary>
    private async Task InstallManagerAsync(string customNodesPath, string managerPath)
    {
        _isInstallingManager = true;

        try
        {
            // 确保 custom_nodes 目录存在
            Directory.CreateDirectory(customNodesPath);

            // 在 UI 线程上创建并显示对话框
            var success = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Views.GitCloneDialog(ManagerGitUrl, ManagerDirName, customNodesPath);
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                dialog.ShowDialog();
                return dialog.IsSuccess;
            });

            if (!success)
            {
                Configuration.Manager.EnableManager = false;
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"安装 ComfyUI-Manager 时发生错误：\n\n{ex.Message}", "安装失败");
            Configuration.Manager.EnableManager = false;
        }
        finally
        {
            _isInstallingManager = false;
        }
    }

    /// <summary>
    /// 生成 extra_model_paths.yaml 文件
    /// </summary>
    private async Task GenerateExtraModelPathsYamlAsync()
    {
        var extraBaseDir = Configuration.Paths.ExtraModelBaseDirectory;
        
        // 如果未设置扩展模型目录，删除现有的 yaml 文件（如果有）
        if (string.IsNullOrWhiteSpace(extraBaseDir))
        {
            if (!string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
            {
                var yamlPath = Path.Combine(_comfyPathService.ComfyUiPath, "extra_model_paths.yaml");
                if (File.Exists(yamlPath))
                {
                    try
                    {
                        File.Delete(yamlPath);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"删除 extra_model_paths.yaml 失败: {yamlPath}", ex);
                    }
                }
            }
            return;
        }

        // 验证目录存在
        if (!Directory.Exists(extraBaseDir))
        {
            _dialogService.ShowError($"扩展模型目录不存在：\n{extraBaseDir}", "路径错误");
            return;
        }

        // 验证 ComfyUI 路径
        if (string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
        {
            _dialogService.ShowError("未检测到有效的 ComfyUI 安装目录。", "路径错误");
            return;
        }

        var yamlFilePath = Path.Combine(_comfyPathService.ComfyUiPath, "extra_model_paths.yaml");

        // 生成 YAML 内容（标准 ComfyUI 目录结构）
        var yamlContent = GenerateStandardYamlContent(extraBaseDir);

        try
        {
            await File.WriteAllTextAsync(yamlFilePath, yamlContent, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"保存 extra_model_paths.yaml 失败：\n\n{ex.Message}", "保存失败");
        }
    }

    /// <summary>
    /// 生成标准的 YAML 配置内容
    /// </summary>
    private static string GenerateStandardYamlContent(string basePath)
    {
        // 规范化路径（统一使用正斜杠）
        var normalizedPath = basePath.Replace('\\', '/');
        if (!normalizedPath.EndsWith('/'))
        {
            normalizedPath += '/';
        }

        return $@"# Auto-generated by ComfyShell
# 扩展模型路径配置

comfyui_extra:
    base_path: {normalizedPath}
    checkpoints: models/checkpoints/
    clip: models/clip/
    clip_vision: models/clip_vision/
    configs: models/configs/
    controlnet: models/controlnet/
    diffusion_models: models/diffusion_models/
    embeddings: models/embeddings/
    loras: models/loras/
    upscale_models: models/upscale_models/
    vae: models/vae/
";
    }
}
