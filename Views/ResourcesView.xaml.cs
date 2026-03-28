using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfDesktop.ViewModels;

namespace WpfDesktop.Views;

/// <summary>
/// ResourcesView.xaml 的交互逻辑
/// </summary>
public partial class ResourcesView : UserControl
{
    public ResourcesView()
    {
        InitializeComponent();
        AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnWorkflowHeaderClick));
    }

    private void OnWorkflowHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Tag is not string propertyName)
        {
            return;
        }

        if (DataContext is ResourcesViewModel viewModel)
        {
            viewModel.SortWorkflows(propertyName);
            e.Handled = true;
        }
    }
}
