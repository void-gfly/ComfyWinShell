using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using WpfDesktop.ViewModels;
using Xunit;

namespace WpfDesktop.Tests.ViewModels;

public sealed class ConfigurationViewModelTests
{
    [Fact]
    public void SyncSelectedCudaDevice_WhenNoMatchingItem_ShowsSavedCudaDevice()
    {
        var configuration = new ComfyConfiguration
        {
            Device = new DeviceConfiguration
            {
                CudaDevice = 1
            }
        };

        var viewModel = new ConfigurationViewModel(
            new FakeConfigurationService(configuration),
            new FakeComfyPathService(),
            new FakeProfileService(),
            new FakeHardwareMonitorService(),
            new FakeComfyManagerSettingsService(),
            new ArgumentBuilder(),
            new FakeDialogService(),
            new FakeLogService());

        viewModel.Configuration = configuration;
        viewModel.CudaDevices.Clear();
        viewModel.CudaDevices.Add(CudaDeviceOption.None);

        typeof(ConfigurationViewModel)
            .GetMethod("SyncSelectedCudaDevice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(viewModel, null);

        Assert.Equal(1, viewModel.Configuration.Device.CudaDevice);
        Assert.Equal(1, viewModel.SelectedCudaDevice?.DeviceId);
        Assert.Contains(viewModel.CudaDevices, device => device.DeviceId == 1);
    }

    [Fact]
    public void SelectedCudaDevice_WhenUserChangesSelection_UpdatesConfiguration()
    {
        var configuration = new ComfyConfiguration
        {
            Device = new DeviceConfiguration()
        };

        var viewModel = new ConfigurationViewModel(
            new FakeConfigurationService(configuration),
            new FakeComfyPathService(),
            new FakeProfileService(),
            new FakeHardwareMonitorService(),
            new FakeComfyManagerSettingsService(),
            new ArgumentBuilder(),
            new FakeDialogService(),
            new FakeLogService());

        viewModel.Configuration = configuration;

        var selected = CudaDeviceOption.Create(1, "NVIDIA GeForce RTX 4070");
        viewModel.SelectedCudaDevice = selected;

        Assert.Equal(1, viewModel.Configuration.Device.CudaDevice);
    }

    [Fact]
    public void SelectedCudaDevice_WhenSelectionBecomesNull_DoesNotClearSavedCudaDeviceAndLogsWarning()
    {
        var configuration = new ComfyConfiguration
        {
            Device = new DeviceConfiguration
            {
                CudaDevice = 1
            }
        };
        var logService = new FakeLogService();
        var viewModel = new ConfigurationViewModel(
            new FakeConfigurationService(configuration),
            new FakeComfyPathService(),
            new FakeProfileService(),
            new FakeHardwareMonitorService(),
            new FakeComfyManagerSettingsService(),
            new ArgumentBuilder(),
            new FakeDialogService(),
            logService);

        viewModel.Configuration = configuration;
        viewModel.SelectedCudaDevice = CudaDeviceOption.Create(1, "NVIDIA GeForce RTX 4070");

        viewModel.SelectedCudaDevice = null;

        Assert.Equal(1, viewModel.Configuration.Device.CudaDevice);
        Assert.Contains(logService.Entries, entry =>
            entry.Level == GUILogLevel.Warning &&
            entry.Message.Contains("CUDA 设备选择被置空", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Constructor_DoesNotStartLoadingConfiguration()
    {
        var configurationService = new FakeConfigurationService(new ComfyConfiguration());
        var viewModel = new ConfigurationViewModel(
            configurationService,
            new FakeComfyPathService(),
            new FakeProfileService(),
            new FakeHardwareMonitorService(),
            new FakeComfyManagerSettingsService(),
            new ArgumentBuilder(),
            new FakeDialogService(),
            new FakeLogService());

        Assert.Equal(0, configurationService.LoadCalls);

        var loadTask = viewModel.OnNavigatedToAsync();
        configurationService.CompleteLoad();
        await loadTask;

        Assert.Equal(1, configurationService.LoadCalls);
    }

    [Fact]
    public async Task OnNavigatedToAsync_WhenSavedCudaDeviceIsNotEnumerated_ShowsSavedCudaDevice()
    {
        var configuration = new ComfyConfiguration
        {
            Device = new DeviceConfiguration
            {
                CudaDevice = 1
            }
        };
        var configurationService = new FakeConfigurationService(configuration);
        var logService = new FakeLogService();
        var viewModel = new ConfigurationViewModel(
            configurationService,
            new FakeComfyPathService(),
            new FakeProfileService(),
            new FakeHardwareMonitorService(),
            new FakeComfyManagerSettingsService(),
            new ArgumentBuilder(),
            new FakeDialogService(),
            logService);

        var loadTask = viewModel.OnNavigatedToAsync();
        configurationService.CompleteLoad();
        await loadTask;

        Assert.Equal(1, viewModel.Configuration.Device.CudaDevice);
        Assert.Equal(1, viewModel.SelectedCudaDevice?.DeviceId);
        Assert.Contains(viewModel.CudaDevices, device => device.DeviceId == 1);
        Assert.Contains(logService.Entries, entry =>
            entry.Level == GUILogLevel.Warning &&
            entry.Message.Contains("CUDA 设备枚举未返回已保存设备", StringComparison.Ordinal));
    }

    private sealed class FakeConfigurationService : IConfigurationService
    {
        private readonly ComfyConfiguration _configuration;
        private readonly TaskCompletionSource<ComfyConfiguration> _loadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeConfigurationService(ComfyConfiguration configuration)
        {
            _configuration = configuration;
        }

        public int LoadCalls { get; private set; }

        public Task<ComfyConfiguration> LoadConfigurationAsync(string profileId)
        {
            LoadCalls++;
            return _loadGate.Task;
        }

        public Task SaveConfigurationAsync(string profileId, ComfyConfiguration configuration)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ValidateConfigurationAsync(ComfyConfiguration configuration)
        {
            return Task.FromResult(true);
        }

        public void CompleteLoad()
        {
            _loadGate.TrySetResult(_configuration);
        }
    }

    private sealed class FakeProfileService : IProfileService
    {
        public Task<IReadOnlyList<Profile>> GetProfilesAsync()
        {
            return Task.FromResult<IReadOnlyList<Profile>>(Array.Empty<Profile>());
        }

        public Task<Profile?> GetProfileAsync(string profileId)
        {
            return Task.FromResult<Profile?>(null);
        }

        public Task<Profile> CreateProfileAsync(string name, string? description = null)
        {
            throw new NotSupportedException();
        }

        public Task SaveProfileAsync(Profile profile)
        {
            return Task.CompletedTask;
        }

        public Task DeleteProfileAsync(string profileId)
        {
            return Task.CompletedTask;
        }

        public Task<bool> SetDefaultProfileAsync(string profileId)
        {
            return Task.FromResult(false);
        }

        public Task<Profile?> ImportProfileAsync(string filePath)
        {
            return Task.FromResult<Profile?>(null);
        }

        public Task ExportProfileAsync(Profile profile, string filePath)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHardwareMonitorService : IHardwareMonitorService
    {
        public HwInfoSnapshot GetSnapshot()
        {
            return new HwInfoSnapshot();
        }

        public bool IsAvailable => true;

        public void Dispose()
        {
        }
    }

    private sealed class FakeComfyPathService : IComfyPathService
    {
        public string? ComfyUiPath => null;
        public string? ComfyRootPath => null;
        public bool IsValid => false;
        public string? ErrorMessage => null;

        public void Refresh()
        {
        }
    }

    private sealed class FakeComfyManagerSettingsService : IComfyManagerSettingsService
    {
        public Task ApplyRemoteCustomNodeInstallAsync(string comfyUiPath, string? userDirectory, bool enabled)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? SelectFolder(string title, string? initialDirectory = null) => null;
        public string? SelectFile(string title, string? filter = null, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string? defaultFileName = null, string? filter = null, string? initialDirectory = null) => null;
        public bool Confirm(string message, string title = "确认") => false;
        public void ShowInfo(string message, string title = "信息") { }
        public void ShowError(string message, string title = "错误") { }
    }

    private sealed class FakeLogService : ILogService
    {
        public event EventHandler<string>? LogReceived;
        public event EventHandler<LogEntry>? LogEntryReceived;

        public List<(string Message, GUILogLevel Level)> Entries { get; } = new();

        public void Log(string message)
        {
            Log(message, GUILogLevel.Info);
        }

        public void Log(string message, GUILogLevel level)
        {
            Entries.Add((message, level));
        }

        public void LogError(string message, Exception? exception = null)
        {
            Entries.Add((message, GUILogLevel.Error));
        }
    }
}
