using System.Windows;
using WpfDesktop.ViewModels;

namespace WpfDesktop.Views;

public partial class GpuPowerWindow : Window
{
    private readonly GpuPowerViewModel _viewModel;

    public GpuPowerWindow(GpuPowerViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) =>
        await _viewModel.RefreshCommand.ExecuteAsync(null);

    private void Window_Closed(object? sender, EventArgs e) => _viewModel.CancelPendingOperations();
}
