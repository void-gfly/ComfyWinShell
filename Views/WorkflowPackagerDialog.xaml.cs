using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Views;

/// <summary>
/// 工作流打包对话框
/// </summary>
public partial class WorkflowPackagerDialog : Window, INotifyPropertyChanged
{
    private readonly IWorkflowPackagerService _packagerService;
    private readonly ILogService _logService;
    private readonly WorkflowAnalysisResult _analysisResult;

    public WorkflowPackagerDialog(
        IWorkflowPackagerService packagerService,
        ILogService logService,
        WorkflowAnalysisResult analysisResult)
    {
        InitializeComponent();
        DataContext = this;

        _packagerService = packagerService;
        _logService = logService;
        _analysisResult = analysisResult;

        // 初始化显示数据
        WorkflowName = analysisResult.WorkflowName;
        TotalNodeCount = analysisResult.TotalNodeCount;
        RequiredModelsCount = analysisResult.RequiredModels.Count;
        MissingNodeCount = analysisResult.MissingNodeCount;
        MissingModelCount = analysisResult.MissingModelCount;

        CheckPackageReadiness();
    }

    #region Properties

    private string _workflowName = "";
    public string WorkflowName
    {
        get => _workflowName;
        set { _workflowName = value; OnPropertyChanged(); }
    }

    private int _totalNodeCount;
    public int TotalNodeCount
    {
        get => _totalNodeCount;
        set { _totalNodeCount = value; OnPropertyChanged(); }
    }

    private int _requiredModelsCount;
    public int RequiredModelsCount
    {
        get => _requiredModelsCount;
        set { _requiredModelsCount = value; OnPropertyChanged(); }
    }

    private int _missingNodeCount;
    public int MissingNodeCount
    {
        get => _missingNodeCount;
        set { _missingNodeCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MissingNodeCountColor)); }
    }

    private int _missingModelCount;
    public int MissingModelCount
    {
        get => _missingModelCount;
        set { _missingModelCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MissingModelCountColor)); }
    }

    public Brush MissingNodeCountColor => MissingNodeCount > 0
        ? new SolidColorBrush(Color.FromRgb(220, 53, 69))  // #DC3545 红色
        : new SolidColorBrush(Color.FromRgb(40, 167, 69)); // #28A745 绿色

    public Brush MissingModelCountColor => MissingModelCount > 0
        ? new SolidColorBrush(Color.FromRgb(220, 53, 69))  // #DC3545 红色
        : new SolidColorBrush(Color.FromRgb(40, 167, 69)); // #28A745 绿色

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

    #endregion

    #region Event Handlers

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // 使用 WPF 的 OpenFolderDialog (需要 Windows 11 或 Windows 10 1809+)
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
        AddLog("开始打包工作流...");

        try
        {
            var progress = new Progress<string>(message =>
            {
                AddLog(message);
            });

            var progressPercentage = new Progress<double>(percentage =>
            {
                PackageProgress = percentage;
            });

            var result = await _packagerService.PackageWorkflowAsync(
                _analysisResult,
                TargetPath,
                progress,
                progressPercentage);

            if (result.Success)
            {
                AddLog("✅ 打包完成！");
                StatusMessage = $"打包成功！目标目录: {TargetPath}";
                MessageBox.Show(
                    $"工作流打包完成！\n\n目标目录: {TargetPath}",
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
            _logService.LogError("工作流打包异常", ex);
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Private Methods

    private void CheckPackageReadiness()
    {
        if (MissingNodeCount > 0 || MissingModelCount > 0)
        {
            StatusMessage = "⚠️ 警告: 工作流存在缺失资源，无法打包！请先安装所有依赖。";
            AddLog("⚠️ 检测到缺失资源:");
            if (MissingNodeCount > 0)
            {
                AddLog($"   - 缺失节点: {MissingNodeCount} 个");
            }
            if (MissingModelCount > 0)
            {
                AddLog($"   - 缺失模型: {MissingModelCount} 个");
            }
            AddLog("请返回工作流分析窗口查看详情，并先安装所有缺失的资源。");
        }
        else
        {
            StatusMessage = "✅ 资源检查通过，可以开始打包。";
            AddLog("✅ 资源检查通过");
            AddLog($"   - 节点: {TotalNodeCount} 个 (全部已安装)");
            AddLog($"   - 模型: {RequiredModelsCount} 个 (全部存在)");
            AddLog("");
            AddLog("请选择打包目标目录，然后点击\"开始打包\"按钮。");
        }

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

        // 检查目录是否为空
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
                // 清空目录
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

    private void UpdateCanStartPackage()
    {
        CanStartPackage = !IsPackaging
            && MissingNodeCount == 0
            && MissingModelCount == 0
            && !string.IsNullOrWhiteSpace(TargetPath);
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogOutput += $"[{timestamp}] {message}\n";

        // 自动滚动到底部
        Dispatcher.InvokeAsync(() =>
        {
            LogScrollViewer.ScrollToEnd();
        });
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
