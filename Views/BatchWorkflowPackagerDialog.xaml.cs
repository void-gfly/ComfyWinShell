using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Views;

/// <summary>
/// 批量工作流打包对话框
/// </summary>
public partial class BatchWorkflowPackagerDialog : Window, INotifyPropertyChanged
{
    private readonly IWorkflowPackagerService _packagerService;
    private readonly ILogService _logService;
    private readonly List<WorkflowAnalysisResult> _analysisResults;

    public BatchWorkflowPackagerDialog(
        IWorkflowPackagerService packagerService,
        ILogService logService,
        List<WorkflowAnalysisResult> analysisResults)
    {
        InitializeComponent();
        DataContext = this;

        _packagerService = packagerService;
        _logService = logService;
        _analysisResults = analysisResults;

        InitializeSummary();
    }

    #region Properties

    public ObservableCollection<string> WorkflowNames { get; } = new();

    private int _workflowCount;
    public int WorkflowCount
    {
        get => _workflowCount;
        set { _workflowCount = value; OnPropertyChanged(); }
    }

    private int _mergedModelsCount;
    public int MergedModelsCount
    {
        get => _mergedModelsCount;
        set { _mergedModelsCount = value; OnPropertyChanged(); }
    }

    private int _missingModelCount;
    public int MissingModelCount
    {
        get => _missingModelCount;
        set { _missingModelCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MissingModelCountColor)); OnPropertyChanged(nameof(ShowIgnoreMissingModels)); }
    }

    private long _installedModelsTotalBytes;
    public long InstalledModelsTotalBytes
    {
        get => _installedModelsTotalBytes;
        set
        {
            _installedModelsTotalBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InstalledModelsTotalSizeDisplay));
        }
    }

    public string InstalledModelsTotalSizeDisplay
    {
        get
        {
            if (InstalledModelsTotalBytes == 0) return "0 GB";
            var gb = InstalledModelsTotalBytes / (1024.0 * 1024.0 * 1024.0);
            return $"{gb:F2} GB";
        }
    }

    public Brush MissingModelCountColor => MissingModelCount > 0
        ? new SolidColorBrush(Color.FromRgb(220, 53, 69))
        : new SolidColorBrush(Color.FromRgb(40, 167, 69));

    public bool ShowIgnoreMissingModels => MissingModelCount > 0;

    private bool _ignoreMissingModels;
    public bool IgnoreMissingModels
    {
        get => _ignoreMissingModels;
        set { _ignoreMissingModels = value; OnPropertyChanged(); UpdateCanStartPackage(); }
    }

    private string _targetPath = "";
    public string TargetPath
    {
        get => _targetPath;
        set { _targetPath = value; OnPropertyChanged(); UpdateCanStartPackage(); }
    }

    private bool _isPackaging;
    public bool IsPackaging
    {
        get => _isPackaging;
        set { _isPackaging = value; OnPropertyChanged(); UpdateCanStartPackage(); }
    }

    private double _packageProgress;
    public double PackageProgress
    {
        get => _packageProgress;
        set { _packageProgress = value; OnPropertyChanged(); }
    }

    private string _logOutput = "";
    public string LogOutput
    {
        get => _logOutput;
        set { _logOutput = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private bool _canStartPackage;
    public bool CanStartPackage
    {
        get => _canStartPackage;
        set { _canStartPackage = value; OnPropertyChanged(); }
    }

    private bool _canPackageModelsOnly;
    public bool CanPackageModelsOnly
    {
        get => _canPackageModelsOnly;
        set { _canPackageModelsOnly = value; OnPropertyChanged(); }
    }

    #endregion

    #region Event Handlers

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择打包目标目录（必须为空目录）",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            TargetPath = dialog.FolderName;
        }
    }

    private async void StartPackageButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateTargetPath())
        {
            return;
        }

        IsPackaging = true;
        PackageProgress = 0;
        LogOutput = "";
        AddLog("开始批量打包工作流...");

        try
        {
            var progress = new Progress<string>(message => AddLog(message));
            var progressPercentage = new Progress<double>(percentage => PackageProgress = percentage);

            var result = await _packagerService.PackageBatchWorkflowsAsync(
                _analysisResults,
                TargetPath,
                progress,
                progressPercentage);

            if (result.Success)
            {
                AddLog("✅ 批量打包完成！");
                StatusMessage = $"打包成功！目标目录: {TargetPath}";
                MessageBox.Show(
                    $"批量工作流打包完成！\n\n目标目录: {TargetPath}",
                    "打包成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                AddLog($"❌ 打包失败: {result.ErrorMessage}");
                StatusMessage = $"打包失败: {result.ErrorMessage}";
                MessageBox.Show(
                    $"打包失败！\n\n错误信息: {result.ErrorMessage}",
                    "打包失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            AddLog($"❌ 打包异常: {ex.Message}");
            StatusMessage = $"打包异常: {ex.Message}";
            _logService.LogError("批量工作流打包异常", ex);
            MessageBox.Show(
                $"打包过程中发生异常！\n\n{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsPackaging = false;
        }
    }

    private async void PackageModelsOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateModelsOnlyTargetPath())
        {
            return;
        }

        IsPackaging = true;
        PackageProgress = 0;
        LogOutput = "";
        AddLog("开始仅打包模型目录...");

        try
        {
            var progress = new Progress<string>(message => AddLog(message));
            var progressPercentage = new Progress<double>(percentage => PackageProgress = percentage);

            var result = await _packagerService.PackageBatchWorkflowModelsOnlyAsync(
                _analysisResults,
                TargetPath,
                progress,
                progressPercentage);

            if (result.Success)
            {
                AddLog("✅ 模型目录导出完成！");
                StatusMessage = $"模型目录导出成功！目标目录: {TargetPath}";
                MessageBox.Show(
                    $"批量模型目录导出完成！\n\n目标目录: {TargetPath}",
                    "导出成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                AddLog($"❌ 导出失败: {result.ErrorMessage}");
                StatusMessage = $"导出失败: {result.ErrorMessage}";
                MessageBox.Show(
                    $"导出失败！\n\n错误信息: {result.ErrorMessage}",
                    "导出失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            AddLog($"❌ 导出异常: {ex.Message}");
            StatusMessage = $"导出异常: {ex.Message}";
            _logService.LogError("批量模型目录导出异常", ex);
            MessageBox.Show(
                $"导出过程中发生异常！\n\n{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsPackaging = false;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Private Methods

    private void InitializeSummary()
    {
        WorkflowCount = _analysisResults.Count;
        WorkflowNames.Clear();
        foreach (var result in _analysisResults)
        {
            WorkflowNames.Add(result.WorkflowName);
        }

        var mergedModels = _analysisResults
            .SelectMany(r => r.RequiredModels)
            .Where(m => !string.IsNullOrWhiteSpace(m.ModelPath))
            .GroupBy(m => m.ModelPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        MergedModelsCount = mergedModels.Count;
        MissingModelCount = mergedModels.Count(m => !m.Exists);
        InstalledModelsTotalBytes = mergedModels.Where(m => m.Exists).Sum(m => m.SizeBytes);

        if (MissingModelCount > 0)
        {
            StatusMessage = "⚠️ 检测到缺失模型，可勾选\"忽略缺失模型\"继续打包。";
            AddLog($"⚠️ 合并后缺失模型: {MissingModelCount} 个");
        }
        else
        {
            StatusMessage = "✅ 模型检查通过，可以开始批量打包。";
            AddLog("✅ 合并模型检查通过");
        }

        AddLog($"工作流数量: {WorkflowCount}");
        AddLog($"合并后模型数量: {MergedModelsCount}");
        AddLog($"已安装模型总大小: {InstalledModelsTotalSizeDisplay}");
        AddLog("请选择打包目标目录，然后点击\"开始打包\"。");

        UpdateCanStartPackage();
    }

    private bool ValidateTargetPath()
    {
        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            MessageBox.Show(
                "请先选择打包目标目录！",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (!Directory.Exists(TargetPath))
        {
            try
            {
                Directory.CreateDirectory(TargetPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法创建目标目录！\n\n{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        if (Directory.GetFiles(TargetPath).Length > 0 || Directory.GetDirectories(TargetPath).Length > 0)
        {
            var result = MessageBox.Show(
                "目标目录不为空！是否清空目录并继续？\n\n警告: 此操作将删除目录中的所有内容！",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return false;
            }

            try
            {
                foreach (var file in Directory.GetFiles(TargetPath))
                {
                    File.Delete(file);
                }

                foreach (var dir in Directory.GetDirectories(TargetPath))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法清空目标目录！\n\n{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        return true;
    }

    private bool ValidateModelsOnlyTargetPath()
    {
        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            MessageBox.Show(
                "请先选择打包目标目录！",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        try
        {
            Directory.CreateDirectory(TargetPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"无法创建目标目录！\n\n{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        return true;
    }

    private void UpdateCanStartPackage()
    {
        CanStartPackage = !IsPackaging
            && (MissingModelCount == 0 || IgnoreMissingModels)
            && !string.IsNullOrWhiteSpace(TargetPath);
        CanPackageModelsOnly = !IsPackaging
            && MergedModelsCount > 0
            && !string.IsNullOrWhiteSpace(TargetPath);
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogOutput += $"[{timestamp}] {message}\n";

        Dispatcher.InvokeAsync(() => LogScrollViewer.ScrollToEnd());
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
