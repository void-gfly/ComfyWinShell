using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class ProfileManagerViewModel : ViewModelBase
{
    private readonly IProfileService _profileService;
    private readonly IDialogService _dialogService;

    public ProfileManagerViewModel(IProfileService profileService, IDialogService dialogService)
    {
        _profileService = profileService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync, CanCreate);
        SetDefaultCommand = new AsyncRelayCommand(SetDefaultAsync, CanOperateOnSelection);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanOperateOnSelection);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => !IsLoading);
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanOperateOnSelection);

        _ = RefreshAsync();
    }

    public ObservableCollection<Profile> Profiles { get; } = new();

    [ObservableProperty]
    private Profile? _selectedProfile;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _newProfileName = "";

    [ObservableProperty]
    private string _newProfileDescription = "";

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand SetDefaultCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    public IAsyncRelayCommand ExportCommand { get; }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            Profiles.Clear();
            var profiles = await _profileService.GetProfilesAsync();
            foreach (var profile in profiles.OrderByDescending(p => p.LastModified ?? p.CreatedAt))
            {
                Profiles.Add(profile);
            }

            StatusMessage = $"已加载 {Profiles.Count} 个配置";
        }
        finally
        {
            IsLoading = false;
            NotifyCommandState();
        }
    }

    private async Task CreateAsync()
    {
        IsLoading = true;
        try
        {
            var profile = await _profileService.CreateProfileAsync(NewProfileName, NewProfileDescription);
            StatusMessage = "已创建配置";
            NewProfileName = string.Empty;
            NewProfileDescription = string.Empty;
            await RefreshAsync();
            SelectedProfile = profile;
        }
        finally
        {
            IsLoading = false;
            NotifyCommandState();
        }
    }

    private async Task SetDefaultAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var success = await _profileService.SetDefaultProfileAsync(SelectedProfile.Id);
            StatusMessage = success ? "已设为默认配置" : "设置默认失败";
            await RefreshAsync();
        }
        finally
        {
            IsLoading = false;
            NotifyCommandState();
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            await _profileService.DeleteProfileAsync(SelectedProfile.Id);
            StatusMessage = "已删除配置";
            await RefreshAsync();
        }
        finally
        {
            IsLoading = false;
            NotifyCommandState();
        }
    }

    private async Task ImportAsync()
    {
        IsLoading = true;
        try
        {
            var filePath = _dialogService.SelectFile("导入配置", "配置文件|*.json|所有文件|*.*");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var profile = await _profileService.ImportProfileAsync(filePath);
            StatusMessage = profile == null ? "导入失败" : "已导入配置";
            await RefreshAsync();
        }
        finally
        {
            IsLoading = false;
            NotifyCommandState();
        }
    }

    private async Task ExportAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var defaultName = string.IsNullOrWhiteSpace(SelectedProfile.Name)
                ? "profile.json"
                : $"{SelectedProfile.Name}.json";

            var filePath = _dialogService.SaveFile("导出配置", defaultName, "配置文件|*.json|所有文件|*.*");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            await _profileService.ExportProfileAsync(SelectedProfile, filePath);
            StatusMessage = "已导出配置";
        }
        finally
        {
            IsLoading = false;
            NotifyCommandState();
        }
    }

    private bool CanOperateOnSelection()
    {
        return SelectedProfile != null && !IsLoading;
    }

    private bool CanCreate()
    {
        return !IsLoading && !string.IsNullOrWhiteSpace(NewProfileName);
    }

    private void NotifyCommandState()
    {
        CreateCommand.NotifyCanExecuteChanged();
        SetDefaultCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProfileChanged(Profile? value)
    {
        NotifyCommandState();
    }

    partial void OnNewProfileNameChanged(string value)
    {
        NotifyCommandState();
    }
}
