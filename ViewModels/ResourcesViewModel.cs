using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;
using WpfDesktop.Views;

namespace WpfDesktop.ViewModels;

public partial class ResourcesViewModel : ViewModelBase, INavigationAware
{
    private readonly IComfyPathService _comfyPathService;
    private readonly IResourceService _resourceService;
    private readonly ILogService _logService;
    private readonly IWorkflowAnalyzerService _workflowAnalyzerService;
    private string _workflowSortProperty = WorkflowSortingHelper.DefaultProperty;
    private ListSortDirection _workflowSortDirection = WorkflowSortingHelper.DefaultDirection;

    public ResourcesViewModel(
        IComfyPathService comfyPathService,
        IResourceService resourceService,
        ILogService logService,
        IWorkflowAnalyzerService workflowAnalyzerService)
    {
        _comfyPathService = comfyPathService;
        _resourceService = resourceService;
        _logService = logService;
        _workflowAnalyzerService = workflowAnalyzerService;
    }

    #region Properties

    public ObservableCollection<CustomNodeInfo> CustomNodes { get; } = new();
    public ObservableCollection<ModelFolderInfo> ModelFolders { get; } = new();
    public ObservableCollection<ModelFolderInfo> ExtraModelFolders { get; } = new();
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
    private int _selectedWorkflowCount;

    [ObservableProperty]
    private string _totalModelSize = "计算中...";

    [ObservableProperty]
    private int _extraModelFoldersCount;

    [ObservableProperty]
    private string _totalExtraModelSize = "计算中...";

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

    [RelayCommand]
    private void AnalyzeWorkflow(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            StatusMessage = "工作流文件不存在";
            return;
        }

        if (!_workflowAnalyzerService.IsWorkflowFile(filePath))
        {
            StatusMessage = "不是有效的工作流文件";
            return;
        }

        try
        {
            var dialog = new WorkflowAnalyzerDialog(_workflowAnalyzerService, filePath)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.Show();
        }
        catch (Exception ex)
        {
            _logService.LogError("打开工作流分析器失败", ex);
            StatusMessage = $"分析失败: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelectAllWorkflows))]
    private void SelectAllWorkflows()
    {
        foreach (var workflow in Workflows.Where(w => !w.IsSelected))
        {
            workflow.IsSelected = true;
        }
    }

    private bool CanSelectAllWorkflows()
    {
        return Workflows.Count > 0 && Workflows.Any(w => !w.IsSelected) && !IsLoading;
    }

    [RelayCommand(CanExecute = nameof(CanUnselectAllWorkflows))]
    private void UnselectAllWorkflows()
    {
        foreach (var workflow in Workflows.Where(w => w.IsSelected))
        {
            workflow.IsSelected = false;
        }
    }

    private bool CanUnselectAllWorkflows()
    {
        return SelectedWorkflowCount > 0 && !IsLoading;
    }

    [RelayCommand(CanExecute = nameof(CanBatchPackageWorkflows))]
    private async Task BatchPackageWorkflowsAsync()
    {
        var selectedWorkflows = Workflows.Where(w => w.IsSelected).ToList();
        if (selectedWorkflows.Count == 0)
        {
            StatusMessage = "请先选择要打包的工作流";
            return;
        }

        var previousStatus = StatusMessage;

        try
        {
            IsLoading = true;
            var analysisResults = new List<WorkflowAnalysisResult>(selectedWorkflows.Count);

            for (var i = 0; i < selectedWorkflows.Count; i++)
            {
                var workflow = selectedWorkflows[i];
                StatusMessage = $"正在分析工作流 ({i + 1}/{selectedWorkflows.Count}): {workflow.Name}";

                if (!_workflowAnalyzerService.IsWorkflowFile(workflow.Path))
                {
                    StatusMessage = $"无效工作流文件: {workflow.Name}";
                    return;
                }

                var result = await _workflowAnalyzerService.AnalyzeWorkflowAsync(workflow.Path);
                if (!result.Success)
                {
                    StatusMessage = $"分析失败: {workflow.Name}, {result.ErrorMessage}";
                    return;
                }

                analysisResults.Add(result);
            }

            var app = (App)Application.Current;
            var packagerService = app.AppHost?.Services.GetRequiredService<IWorkflowPackagerService>();
            if (packagerService == null)
            {
                StatusMessage = "无法获取打包服务";
                return;
            }

            var dialog = new BatchWorkflowPackagerDialog(packagerService, _logService, analysisResults)
            {
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();
            StatusMessage = $"批量分析完成，共 {analysisResults.Count} 个工作流";
        }
        catch (Exception ex)
        {
            _logService.LogError("批量工作流分析失败", ex);
            StatusMessage = $"批量分析失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            if (string.IsNullOrWhiteSpace(StatusMessage))
            {
                StatusMessage = previousStatus;
            }
        }
    }

    private bool CanBatchPackageWorkflows()
    {
        return SelectedWorkflowCount > 0 && !IsLoading;
    }

    #endregion

    #region INavigationAware

    public async Task OnNavigatedToAsync()
    {
        await RefreshAsync();
    }

    #endregion

    #region Private Methods

    public void SortWorkflows(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName) || Workflows.Count == 0)
        {
            return;
        }

        _workflowSortDirection = WorkflowSortingHelper.GetNextDirection(propertyName, _workflowSortProperty, _workflowSortDirection);
        _workflowSortProperty = propertyName;
        ApplyWorkflowSorting();
    }

    private void ClearData()
    {
        DetachWorkflowSelectionHandlers(Workflows);
        CustomNodes.Clear();
        ModelFolders.Clear();
        ExtraModelFolders.Clear();
        Workflows.Clear();
        CustomNodesCount = 0;
        ModelFoldersCount = 0;
        ExtraModelFoldersCount = 0;
        WorkflowsCount = 0;
        SelectedWorkflowCount = 0;
        TotalModelSize = "0 GB";
        TotalExtraModelSize = "0 GB";
        RefreshWorkflowCommandStates();
    }

    private async Task LoadAllResourcesAsync()
    {
        StatusMessage = "正在加载自定义节点...";
        await LoadCustomNodesAsync();

        StatusMessage = "正在加载模型文件夹...";
        await LoadModelFoldersAsync();

        StatusMessage = "正在加载扩展模型文件夹...";
        await LoadExtraModelFoldersAsync();

        StatusMessage = "正在加载工作流...";
        await LoadWorkflowsAsync();

        StatusMessage = $"已加载 {CustomNodesCount} 个节点, {ModelFoldersCount} 个模型目录, {ExtraModelFoldersCount} 个扩展模型目录, {WorkflowsCount} 个工作流";

        // 后台计算模型文件夹大小
        _ = CalculateFolderSizesAsync(ModelFolders, false);
        _ = CalculateFolderSizesAsync(ExtraModelFolders, true);
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

    private async Task LoadExtraModelFoldersAsync()
    {
        RunOnUiThread(() => ExtraModelFolders.Clear());

        var folders = await _resourceService.GetExtraModelFoldersAsync();
        var descriptions = await _resourceService.GetModelDescriptionsAsync();

        RunOnUiThread(() =>
        {
            foreach (var folder in folders)
            {
                if (descriptions.TryGetValue(folder.Name, out var description))
                {
                    folder.Description = description;
                }

                ExtraModelFolders.Add(folder);
            }

            ExtraModelFoldersCount = ExtraModelFolders.Count;
            TotalExtraModelSize = "计算中...";
        });
    }

    private async Task LoadWorkflowsAsync()
    {
        RunOnUiThread(() =>
        {
            DetachWorkflowSelectionHandlers(Workflows);
            Workflows.Clear();
        });

        var workflows = await _resourceService.GetWorkflowsAsync();
        var sortedWorkflows = WorkflowSortingHelper.Sort(
            workflows,
            WorkflowSortingHelper.DefaultProperty,
            WorkflowSortingHelper.DefaultDirection);

        RunOnUiThread(() =>
        {
            _workflowSortProperty = WorkflowSortingHelper.DefaultProperty;
            _workflowSortDirection = WorkflowSortingHelper.DefaultDirection;

            foreach (var workflow in sortedWorkflows)
            {
                Workflows.Add(workflow);
                workflow.PropertyChanged += OnWorkflowPropertyChanged;
            }
            WorkflowsCount = Workflows.Count;
            UpdateSelectedWorkflowCount();
            RefreshWorkflowCommandStates();
        });
    }

    private void ApplyWorkflowSorting()
    {
        var sortedWorkflows = WorkflowSortingHelper.Sort(Workflows, _workflowSortProperty, _workflowSortDirection);

        Workflows.Clear();
        foreach (var workflow in sortedWorkflows)
        {
            Workflows.Add(workflow);
        }
    }

    partial void OnSelectedWorkflowCountChanged(int value)
    {
        BatchPackageWorkflowsCommand.NotifyCanExecuteChanged();
        SelectAllWorkflowsCommand.NotifyCanExecuteChanged();
        UnselectAllWorkflowsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        BatchPackageWorkflowsCommand.NotifyCanExecuteChanged();
        SelectAllWorkflowsCommand.NotifyCanExecuteChanged();
        UnselectAllWorkflowsCommand.NotifyCanExecuteChanged();
    }

    private void OnWorkflowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowInfo.IsSelected))
        {
            UpdateSelectedWorkflowCount();
        }
    }

    private void DetachWorkflowSelectionHandlers(IEnumerable<WorkflowInfo> workflows)
    {
        foreach (var workflow in workflows)
        {
            workflow.PropertyChanged -= OnWorkflowPropertyChanged;
        }
    }

    private void UpdateSelectedWorkflowCount()
    {
        SelectedWorkflowCount = Workflows.Count(w => w.IsSelected);
    }

    private void RefreshWorkflowCommandStates()
    {
        BatchPackageWorkflowsCommand.NotifyCanExecuteChanged();
        SelectAllWorkflowsCommand.NotifyCanExecuteChanged();
        UnselectAllWorkflowsCommand.NotifyCanExecuteChanged();
    }

    private async Task CalculateFolderSizesAsync(ObservableCollection<ModelFolderInfo> folders, bool isExtraModel)
    {
        // 创建副本以避免集合修改问题
        var snapshot = folders.ToList();

        // 并发计算所有文件夹大小
        var tasks = snapshot.Select(async folder =>
        {
            var (sizeBytes, fileCount) = await _resourceService.CalculateFolderSizeAsync(folder.Path);

            // 更新单个文件夹信息
            RunOnUiThread(() =>
            {
                var index = folders.ToList().FindIndex(f => f.Path == folder.Path);
                if (index >= 0)
                {
                    folders[index] = new ModelFolderInfo
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
            var sizeText = $"{totalSize / (1024.0 * 1024.0 * 1024.0):F2} GB";
            if (isExtraModel)
            {
                TotalExtraModelSize = sizeText;
            }
            else
            {
                TotalModelSize = sizeText;
            }

            // 按磁盘占用大小从大到小排序
            var sortedFolders = folders.OrderByDescending(f => f.SizeBytes).ToList();
            folders.Clear();
            foreach (var folder in sortedFolders)
            {
                folders.Add(folder);
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
