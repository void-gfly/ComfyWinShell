using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class ResourcesViewModel : ViewModelBase, INavigationAware
{
    private readonly IComfyPathService _comfyPathService;
    private readonly IResourceService _resourceService;
    private readonly ILogService _logService;

    public ResourcesViewModel(
        IComfyPathService comfyPathService,
        IResourceService resourceService,
        ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _resourceService = resourceService;
        _logService = logService;
    }

    #region Properties

    public ObservableCollection<CustomNodeInfo> CustomNodes { get; } = new();
    public ObservableCollection<ModelFolderInfo> ModelFolders { get; } = new();
    public ObservableCollection<WorkflowInfo> Workflows { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isComfyUiFound;

    [ObservableProperty]
    private string _notFoundMessage = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _comfyUiPath = "";

    [ObservableProperty]
    private int _customNodesCount;

    [ObservableProperty]
    private int _modelFoldersCount;

    [ObservableProperty]
    private int _workflowsCount;

    [ObservableProperty]
    private string _totalModelSize = "计算中...";

    #endregion

    #region Commands

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "正在检测 ComfyUI...";

        try
        {
            _comfyPathService.Refresh();

            if (!_comfyPathService.IsValid)
            {
                IsComfyUiFound = false;
                NotFoundMessage = _comfyPathService.ErrorMessage ?? "未找到 ComfyUI";
                StatusMessage = "未找到 ComfyUI";
                ClearData();
                return;
            }

            IsComfyUiFound = true;
            ComfyUiPath = _comfyPathService.ComfyUiPath!;
            NotFoundMessage = "";

            await LoadAllResourcesAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError("刷新资源失败", ex);
            StatusMessage = $"刷新失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenFolder(string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = "文件夹不存在";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logService.LogError("打开文件夹失败", ex);
            StatusMessage = $"打开失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            StatusMessage = "文件不存在";
            return;
        }

        try
        {
            // 在文件管理器中选中文件
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logService.LogError("打开文件失败", ex);
            StatusMessage = $"打开失败: {ex.Message}";
        }
    }

    #endregion

    #region INavigationAware

    public async Task OnNavigatedToAsync()
    {
        await RefreshAsync();
    }

    #endregion

    #region Private Methods

    private void ClearData()
    {
        CustomNodes.Clear();
        ModelFolders.Clear();
        Workflows.Clear();
        CustomNodesCount = 0;
        ModelFoldersCount = 0;
        WorkflowsCount = 0;
        TotalModelSize = "0 GB";
    }

    private async Task LoadAllResourcesAsync()
    {
        StatusMessage = "正在加载自定义节点...";
        await LoadCustomNodesAsync();

        StatusMessage = "正在加载模型文件夹...";
        await LoadModelFoldersAsync();

        StatusMessage = "正在加载工作流...";
        await LoadWorkflowsAsync();

        StatusMessage = $"已加载 {CustomNodesCount} 个节点, {ModelFoldersCount} 个模型目录, {WorkflowsCount} 个工作流";

        // 后台计算模型文件夹大小
        _ = CalculateModelFolderSizesAsync();
    }

    private async Task LoadCustomNodesAsync()
    {
        RunOnUiThread(() => CustomNodes.Clear());

        var nodes = await _resourceService.GetCustomNodesAsync();
        RunOnUiThread(() =>
        {
            foreach (var node in nodes)
            {
                CustomNodes.Add(node);
            }
            CustomNodesCount = CustomNodes.Count;
        });
    }

    private async Task LoadModelFoldersAsync()
    {
        RunOnUiThread(() => ModelFolders.Clear());

        var folders = await _resourceService.GetModelFoldersAsync();
        var descriptions = await _resourceService.GetModelDescriptionsAsync();

        RunOnUiThread(() =>
        {
            foreach (var folder in folders)
            {
                // 查找描述，不区分大小写
                if (descriptions.TryGetValue(folder.Name, out var description))
                {
                    folder.Description = description;
                }

                ModelFolders.Add(folder);
            }
            ModelFoldersCount = ModelFolders.Count;
            TotalModelSize = "计算中...";
        });
    }

    private async Task LoadWorkflowsAsync()
    {
        RunOnUiThread(() => Workflows.Clear());

        var workflows = await _resourceService.GetWorkflowsAsync();
        RunOnUiThread(() =>
        {
            foreach (var workflow in workflows)
            {
                Workflows.Add(workflow);
            }
            WorkflowsCount = Workflows.Count;
        });
    }

    private async Task CalculateModelFolderSizesAsync()
    {
        // 创建副本以避免集合修改问题
        var folders = ModelFolders.ToList();

        // 并发计算所有文件夹大小
        var tasks = folders.Select(async folder =>
        {
            var (sizeBytes, fileCount) = await _resourceService.CalculateFolderSizeAsync(folder.Path);

            // 更新单个文件夹信息
            RunOnUiThread(() =>
            {
                var index = ModelFolders.ToList().FindIndex(f => f.Path == folder.Path);
                if (index >= 0)
                {
                    ModelFolders[index] = new ModelFolderInfo
                    {
                        Name = folder.Name,
                        Path = folder.Path,
                        SizeBytes = sizeBytes,
                        FileCount = fileCount,
                        IsCalculating = false,
                        Description = folder.Description // 保留描述
                    };
                }
            });

            return sizeBytes;
        }).ToList();

        // 等待所有任务完成
        var sizes = await Task.WhenAll(tasks);
        var totalSize = sizes.Sum();

        RunOnUiThread(() =>
        {
            TotalModelSize = $"{totalSize / (1024.0 * 1024.0 * 1024.0):F2} GB";

            // 按磁盘占用大小从大到小排序
            var sortedFolders = ModelFolders.OrderByDescending(f => f.SizeBytes).ToList();
            ModelFolders.Clear();
            foreach (var folder in sortedFolders)
            {
                ModelFolders.Add(folder);
            }
        });
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

    #endregion
}
