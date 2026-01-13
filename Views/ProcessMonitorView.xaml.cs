using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfDesktop.ViewModels;

namespace WpfDesktop.Views;

public partial class ProcessMonitorView : UserControl
{
    private ScrollViewer? _scrollViewer;

    public ProcessMonitorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProcessMonitorViewModel vm)
        {
            vm.Logs.CollectionChanged += OnLogsCollectionChanged;

            // 延迟执行以确保 Visual Tree 和 Layout 就绪
            Dispatcher.InvokeAsync(() =>
            {
                // 尝试获取 ScrollViewer
                _scrollViewer ??= GetScrollViewer(LogListBox);

                // 优先处理 AutoScroll：如果启用了自动滚动，则切换回来时直接滚动到底部
                // 这符合控制台/日志监控的直觉：用户希望看到最新内容
                // 同时也解决了"重置到第一页"的Bug
                if (vm.AutoScroll && LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                }
                // 如果未启用自动滚动，则恢复之前的滚动位置
                else if (_scrollViewer != null)
                {
                    _scrollViewer.ScrollToVerticalOffset(vm.SavedScrollOffset);
                }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProcessMonitorViewModel vm)
        {
            vm.Logs.CollectionChanged -= OnLogsCollectionChanged;

            // 保存滚动位置
            if (_scrollViewer != null)
            {
                vm.SavedScrollOffset = _scrollViewer.VerticalOffset;
            }
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is ProcessMonitorViewModel { AutoScroll: true } && e.Action == NotifyCollectionChangedAction.Add)
        {
            if (LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
            }
        }
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject depObj)
    {
        if (depObj is ScrollViewer sv) return sv;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }
}

