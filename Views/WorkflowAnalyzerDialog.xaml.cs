using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Views;

/// <summary>
/// 工作流分析对话框
/// </summary>
public partial class WorkflowAnalyzerDialog : Window, INotifyPropertyChanged
{
    private readonly IWorkflowAnalyzerService _analyzerService;
    private readonly string _workflowPath;
    private WorkflowAnalysisResult? _analysisResult;

    public WorkflowAnalyzerDialog(IWorkflowAnalyzerService analyzerService, string workflowPath)
    {
        InitializeComponent();
        DataContext = this;

        _analyzerService = analyzerService;
        _workflowPath = workflowPath;

        Loaded += OnLoaded;
    }

    #region Properties

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string _workflowName = "";
    public string WorkflowName
    {
        get => _workflowName;
        set { _workflowName = value; OnPropertyChanged(); }
    }

    private string _workflowPath2 = "";
    public string WorkflowPath
    {
        get => _workflowPath2;
        set { _workflowPath2 = value; OnPropertyChanged(); }
    }

    private string _format = "";
    public string Format
    {
        get => _format;
        set { _format = value; OnPropertyChanged(); }
    }

    private int _totalNodeCount;
    public int TotalNodeCount
    {
        get => _totalNodeCount;
        set { _totalNodeCount = value; OnPropertyChanged(); }
    }

    private int _missingModelCount;
    public int MissingModelCount
    {
        get => _missingModelCount;
        set { _missingModelCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MissingCountColor)); }
    }

    private int _missingNodeCount;
    public int MissingNodeCount
    {
        get => _missingNodeCount;
        set { _missingNodeCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MissingCountColor)); }
    }

    public int RequiredModelsCount => RequiredModels.Count;

    public Brush MissingCountColor => (MissingNodeCount + MissingModelCount) > 0 
        ? new SolidColorBrush(Color.FromRgb(220, 53, 69))  // #DC3545 红色
        : new SolidColorBrush(Color.FromRgb(40, 167, 69)); // #28A745 绿色

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ObservableCollection<RequiredCustomNode> RequiredNodes { get; } = new();
    public ObservableCollection<RequiredModel> RequiredModels { get; } = new();

    #endregion

    #region Event Handlers

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await AnalyzeWorkflowAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // 打包按钮点击事件
    private void PackageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_analysisResult == null)
        {
            MessageBox.Show(
                "分析结果不可用，无法打包！",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // 获取服务
        var app = (App)Application.Current;
        var packagerService = app.AppHost?.Services.GetRequiredService<IWorkflowPackagerService>();
        var logService = app.AppHost?.Services.GetRequiredService<ILogService>();

        if (packagerService == null || logService == null)
        {
            MessageBox.Show(
                "无法获取打包服务！",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // 关闭当前窗口
        Close();

        // 打开打包窗口
        var packagerDialog = new WorkflowPackagerDialog(packagerService, logService, _analysisResult)
        {
            Owner = Owner
        };
        packagerDialog.ShowDialog();
    }

    // 节点右键菜单：复制节点名
    private void CopyNodeName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.CommandParameter is RequiredCustomNode node)
        {
            CopyToClipboard(node.NodeType, "节点名");
        }
        else if (TryGetSelectedNode(out var selectedNode))
        {
            CopyToClipboard(selectedNode.NodeType, "节点名");
        }
    }

    // 节点右键菜单：搜索节点名
    private void SearchNodeName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.CommandParameter is RequiredCustomNode node)
        {
            SearchOnGoogle(node.NodeType);
        }
        else if (TryGetSelectedNode(out var selectedNode))
        {
            SearchOnGoogle(selectedNode.NodeType);
        }
    }

    // 模型右键菜单：复制模型名
    private void CopyModelName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.CommandParameter is RequiredModel model)
        {
            CopyToClipboard(model.ModelName, "模型名");
        }
        else if (TryGetSelectedModel(out var selectedModel))
        {
            CopyToClipboard(selectedModel.ModelName, "模型名");
        }
    }

    // 模型右键菜单：复制模型路径
    private void CopyModelPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.CommandParameter is RequiredModel model)
        {
            var pathToCopy = !string.IsNullOrEmpty(model.FullPath) ? model.FullPath : model.ModelPath;
            CopyToClipboard(pathToCopy, "模型路径");
        }
        else if (TryGetSelectedModel(out var selectedModel))
        {
            var pathToCopy = !string.IsNullOrEmpty(selectedModel.FullPath) ? selectedModel.FullPath : selectedModel.ModelPath;
            CopyToClipboard(pathToCopy, "模型路径");
        }
    }

    // 模型右键菜单：搜索模型名
    private void SearchModelName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.CommandParameter is RequiredModel model)
        {
            SearchOnGoogle(model.ModelName);
        }
        else if (TryGetSelectedModel(out var selectedModel))
        {
            SearchOnGoogle(selectedModel.ModelName);
        }
    }

    #endregion

    #region Private Methods

    private async Task AnalyzeWorkflowAsync()
    {
        IsLoading = true;
        StatusMessage = "正在分析工作流...";

        try
        {
            var result = await _analyzerService.AnalyzeWorkflowAsync(_workflowPath);
            _analysisResult = result; // 保存分析结果

            if (result.Success)
            {
                WorkflowName = result.WorkflowName;
                WorkflowPath = result.WorkflowPath;
                Format = result.Format;
                TotalNodeCount = result.TotalNodeCount;
                MissingModelCount = result.MissingModelCount;
                MissingNodeCount = result.MissingNodeCount;

                RequiredNodes.Clear();
                foreach (var node in result.RequiredNodes)
                {
                    RequiredNodes.Add(node);
                }

                RequiredModels.Clear();
                foreach (var model in result.RequiredModels)
                {
                    RequiredModels.Add(model);
                }

                OnPropertyChanged(nameof(RequiredModelsCount));

                var customNodeCount = result.RequiredNodes.Count(n => !n.IsBuiltIn);
                StatusMessage = $"分析完成: {result.RequiredNodes.Count} 个节点类型, {customNodeCount} 个自定义节点, {result.RequiredModels.Count} 个模型依赖";
            }
            else
            {
                StatusMessage = $"分析失败: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"分析失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 复制到剪贴板
    /// </summary>
    private void CopyToClipboard(string text, string typeName)
    {
        try
        {
            Clipboard.SetText(text);
            StatusMessage = $"已复制{typeName}: {text}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"复制失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 在浏览器中搜索
    /// </summary>
    private void SearchOnGoogle(string searchTerm)
    {
        try
        {
            var url = $"https://www.google.com/search?q=ComfyUI+{Uri.EscapeDataString(searchTerm)}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusMessage = $"正在搜索: {searchTerm}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 尝试获取选中的节点
    /// </summary>
    private bool TryGetSelectedNode(out RequiredCustomNode node)
    {
        node = null!;
        var listView = FindNodeListView();
        if (listView?.SelectedItem is RequiredCustomNode selectedNode)
        {
            node = selectedNode;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 尝试获取选中的模型
    /// </summary>
    private bool TryGetSelectedModel(out RequiredModel model)
    {
        model = null!;
        var listView = FindModelListView();
        if (listView?.SelectedItem is RequiredModel selectedModel)
        {
            model = selectedModel;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 查找节点 ListView
    /// </summary>
    private System.Windows.Controls.ListView? FindNodeListView()
    {
        return FindName("NodeListView") as System.Windows.Controls.ListView;
    }

    /// <summary>
    /// 查找模型 ListView
    /// </summary>
    private System.Windows.Controls.ListView? FindModelListView()
    {
        return FindName("ModelListView") as System.Windows.Controls.ListView;
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
