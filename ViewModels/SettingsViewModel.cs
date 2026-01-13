using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsLoading);

        _ = LoadAsync();
    }

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private bool _isLoading;

    public IAsyncRelayCommand SaveCommand { get; }

    public IReadOnlyList<string> LanguageOptions { get; } = new[] { "zh-CN" };

    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "Dark" };

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
