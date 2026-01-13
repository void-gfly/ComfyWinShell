using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class VersionManagerViewModel : ViewModelBase
{
    private readonly IComfyPathService _comfyPathService;
    private readonly IGitService _gitService;
    private readonly IPythonPathService _pythonPathService;
    private readonly IProcessService _processService;
    private readonly ILogService _logService;

    public VersionManagerViewModel(
        IComfyPathService comfyPathService,
        IGitService gitService,
        IPythonPathService pythonPathService,
        IProcessService processService,
        ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _gitService = gitService;
        _pythonPathService = pythonPathService;
        _processService = processService;
        _logService = logService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SwitchVersionCommand = new AsyncRelayCommand<GitCommit>(SwitchVersionAsync);

        _ = RefreshAsync();
    }

    public ObservableCollection<GitCommit> StableVersions { get; } = new();
    public ObservableCollection<GitCommit> DevVersions { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _remoteUrl = "";

    [ObservableProperty]
    private string _currentBranch = "";

    [ObservableProperty]
    private string _currentVersionHash = "";

    [ObservableProperty]
    private string _comfyUiPath = "";

    [ObservableProperty]
    private bool _isComfyUiFound;

    [ObservableProperty]
    private string _notFoundMessage = "";

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<GitCommit> SwitchVersionCommand { get; }

    private async Task RefreshAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "正在检测 ComfyUI...";

        try
        {
            // 刷新路径检测
            _comfyPathService.Refresh();

            if (!_comfyPathService.IsValid)
            {
                IsComfyUiFound = false;
                NotFoundMessage = _comfyPathService.ErrorMessage ?? "未找到 ComfyUI";
                StatusMessage = "未找到 ComfyUI";
                ClearGitInfo();
                return;
            }

            IsComfyUiFound = true;
            ComfyUiPath = _comfyPathService.ComfyUiPath!;
            NotFoundMessage = "";

            await LoadGitInfoAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearGitInfo()
    {
        StableVersions.Clear();
        DevVersions.Clear();
        RemoteUrl = "";
        CurrentBranch = "";
        CurrentVersionHash = "";
    }

    private async Task LoadGitInfoAsync()
    {
        ClearGitInfo();

        var path = ComfyUiPath;

        if (!await _gitService.IsGitRepositoryAsync(path))
        {
            StatusMessage = "ComfyUI 目录不是 Git 仓库";
            return;
        }

        StatusMessage = "正在获取 Git 信息...";

        try
        {
            // 尝试 fetch，但不阻塞（可能没有网络）
            try
            {
                await _gitService.FetchAsync(path);
            }
            catch
            {
                // 忽略 fetch 失败，继续读取本地信息
            }

            RemoteUrl = await _gitService.GetRemoteUrlAsync(path);
            CurrentBranch = await _gitService.GetCurrentBranchAsync(path);
            CurrentVersionHash = await _gitService.GetCurrentCommitHashAsync(path);

            var tags = await _gitService.GetTagsAsync(path);
            foreach (var tag in tags)
            {
                StableVersions.Add(tag);
            }

            var commits = await _gitService.GetCommitsAsync(path);
            foreach (var commit in commits)
            {
                DevVersions.Add(commit);
            }

            StatusMessage = $"已加载 {StableVersions.Count} 个标签, {DevVersions.Count} 个提交";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Git 信息加载失败: {ex.Message}";
        }
    }

    private async Task SwitchVersionAsync(GitCommit? commit)
    {
        if (commit == null || string.IsNullOrEmpty(ComfyUiPath)) return;

        // 检查 ComfyUI 是否正在运行
        var status = await _processService.GetStatusAsync();
        if (status?.IsRunning == true)
        {
            StatusMessage = "请先停止 ComfyUI 再切换版本";
            return;
        }

        IsLoading = true;
        try
        {
            // Prefer tag name, fallback to hash
            var targetRef = !string.IsNullOrEmpty(commit.Tag) ? commit.Tag : commit.Hash;

            _logService.Log($"[版本管理] 正在切换到 {targetRef}...");
            StatusMessage = $"正在切换到 {targetRef}...";
            await _gitService.CheckoutAsync(ComfyUiPath, targetRef);

            _logService.Log($"[版本管理] 已切换到 {targetRef}，正在更新依赖...");
            StatusMessage = $"已切换到 {targetRef}，正在更新依赖...";

            // 切换版本后更新依赖
            await UpdateDependenciesAsync();

            _logService.Log($"[版本管理] 版本切换完成: {targetRef}");
            StatusMessage = $"已切换到 {targetRef}，依赖更新完成";
        }
        catch (Exception ex)
        {
            _logService.LogError($"[版本管理] 切换版本失败", ex);
            StatusMessage = $"切换版本失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }

        // 刷新需要在 IsLoading = false 之后调用
        await RefreshAsync();
    }

    private async Task UpdateDependenciesAsync()
    {
        var requirementsPath = Path.Combine(ComfyUiPath, "requirements.txt");
        if (!File.Exists(requirementsPath))
        {
            _logService.Log("[版本管理] 未找到 requirements.txt，跳过依赖更新");
            return;
        }

        // 解析 Python 路径
        _pythonPathService.Resolve(_comfyPathService.ComfyRootPath!);
        if (!_pythonPathService.IsValid)
        {
            _logService.Log("[版本管理] 未找到 Python，跳过依赖更新");
            StatusMessage = "未找到 Python，跳过依赖更新";
            return;
        }

        var pythonPath = _pythonPathService.PythonPath!;
        _logService.Log($"[版本管理] 执行: pip install -r requirements.txt");

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"-m pip install -r \"{requirementsPath}\"",
            WorkingDirectory = ComfyUiPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logService.Log(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logService.Log(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            _logService.Log("[版本管理] 依赖更新完成");
        }
        else
        {
            _logService.Log($"[版本管理] 依赖更新失败，退出码: {process.ExitCode}");
        }
    }
}
