using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;

    public SettingsViewModel(ISettingsService settingsService, ILogService logService)
    {
        _settingsService = settingsService;
        _logService = logService;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsLoading);

        BackgroundTaskObserver.Observe(LoadAsync(), _logService, "加载设置");
    }

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private bool _isLoading;

    public IAsyncRelayCommand SaveCommand { get; }

    public IReadOnlyList<string> LanguageOptions { get; } = new[] { "zh-CN" };

    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "Dark" };

    public IReadOnlyList<ConsoleLineHeightOption> LogLineHeightOptions { get; } = new[]
    {
        new ConsoleLineHeightOption("紧凑", 12),
        new ConsoleLineHeightOption("默认", 15),
        new ConsoleLineHeightOption("宽松", 18)
    };

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Settings = await _settingsService.LoadAsync();
        }
        finally
        {
            IsLoading = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task SaveAsync()
    {
        IsLoading = true;
        try
        {
            await _settingsService.SaveAsync(Settings);

            // Notify other viewmodels or main window to update title
            WeakReferenceMessenger.Default.Send(new AppSettingsChangedMessage(Settings));
        }
        finally
        {
            IsLoading = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}

public sealed class ConsoleLineHeightOption
{
    public ConsoleLineHeightOption(string label, int value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public int Value { get; }
}
