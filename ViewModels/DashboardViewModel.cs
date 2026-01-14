using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase, INavigationAware
{
    private readonly IComfyPathService _comfyPathService;
    private readonly IProfileService _profileService;
    private readonly IConfigurationService _configurationService;
    private readonly IPythonPathService _pythonPathService;
    private readonly ArgumentBuilder _argumentBuilder;
    private readonly ILogService _logService;

    public DashboardViewModel(
        IComfyPathService comfyPathService,
        IProfileService profileService,
        IConfigurationService configurationService,
        IPythonPathService pythonPathService,
        ArgumentBuilder argumentBuilder,
        ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _profileService = profileService;
        _configurationService = configurationService;
        _pythonPathService = pythonPathService;
        _argumentBuilder = argumentBuilder;
        _logService = logService;

        // 构造函数中不再自动刷新，由导航事件触发
    }

    [ObservableProperty]
    private string _activeVersionName = "未检测";

    [ObservableProperty]
    private string _activeVersionPath = "未设置";

    [ObservableProperty]
    private string _activeProfileName = "默认配置";

    [ObservableProperty]
    private string _activePythonPath = "未检测";

    [ObservableProperty]
    private bool _isPythonValid = true;

    [ObservableProperty]
    private bool _isComfyUiFound;

    [ObservableProperty]
    private string _generatedCommand = string.Empty;

    public async Task OnNavigatedToAsync()
    {
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        _comfyPathService.Refresh();

        IsComfyUiFound = _comfyPathService.IsValid;

        if (_comfyPathService.IsValid)
        {
            ActiveVersionName = "ComfyUI";
            ActiveVersionPath = _comfyPathService.ComfyUiPath ?? "未设置";

            var rootPath = _comfyPathService.ComfyRootPath!;
            _pythonPathService.Resolve(rootPath);

            if (!string.IsNullOrEmpty(_pythonPathService.PythonPath))
            {
                ActivePythonPath = _pythonPathService.PythonPath;
                IsPythonValid = true;
            }
            else
            {
                ActivePythonPath = "未找到 Python 环境";
                IsPythonValid = false;
            }
        }
        else
        {
            ActiveVersionName = "未找到";
            ActiveVersionPath = _comfyPathService.ErrorMessage ?? "请将本应用放置在 ComfyUI 同级目录";
            ActivePythonPath = "未检测";
            IsPythonValid = true; // 保持默认颜色，因为此时并未尝试检测 Python
        }

        var profiles = await _profileService.GetProfilesAsync();
        var defaultProfile = profiles.FirstOrDefault(profile => profile.IsDefault);
        ActiveProfileName = defaultProfile?.Name ?? "默认配置";

        await UpdateGeneratedCommandAsync(defaultProfile?.Id ?? "default");
    }

    private async Task UpdateGeneratedCommandAsync(string profileId)
    {
        if (!_comfyPathService.IsValid)
        {
            GeneratedCommand = "# 未找到 ComfyUI 目录";
            return;
        }

        var configuration = await _configurationService.LoadConfigurationAsync(profileId);
        var rootPath = _comfyPathService.ComfyRootPath!;
        var arguments = _argumentBuilder.BuildArguments(configuration);

        // 构建完整命令行
        _pythonPathService.Resolve(rootPath);
        var pythonPath = _pythonPathService.PythonPath;
        var mainPath = ResolveMainPath(rootPath);

        if (pythonPath == null || mainPath == null)
        {
            GeneratedCommand = "# 无法定位 Python 或 main.py";
            return;
        }

        // 格式化为可读的命令行
        var argsStr = string.IsNullOrWhiteSpace(arguments) ? "" : $" {arguments}";
        GeneratedCommand = $"{pythonPath} -s {mainPath} --windows-standalone-build{argsStr}";
    }

    [RelayCommand]
    private void OpenFolder(string folderName)
    {
        if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
        {
            return;
        }

        var path = Path.Combine(_comfyPathService.ComfyUiPath, folderName);

        // Handle nested paths correctly if they use forward slashes
        path = path.Replace('/', Path.DirectorySeparatorChar);

        if (!Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                _logService.LogError($"创建目录失败: {path}", ex);
                return;
            }
        }

        try
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            _logService.LogError($"打开资源管理器失败: {path}", ex);
        }
    }

    private static string? ResolveMainPath(string rootPath)
    {
        var comfyMain = Path.Combine(rootPath, "ComfyUI", "main.py");
        if (File.Exists(comfyMain))
            return comfyMain;

        var rootMain = Path.Combine(rootPath, "main.py");
        if (File.Exists(rootMain))
            return rootMain;

        return null;
    }
}
