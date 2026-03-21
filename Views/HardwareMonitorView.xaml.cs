using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LiveChartsCore.SkiaSharpView.WPF;
using WpfDesktop.ViewModels;

namespace WpfDesktop.Views;

public partial class HardwareMonitorView : UserControl
{
    public HardwareMonitorView()
    {
        InitializeComponent();
        Loaded += (_, _) => TryWireCpuMemoryCharts();
        DataContextChanged += (_, _) => TryWireCpuMemoryCharts();
    }

    private void TryWireCpuMemoryCharts()
    {
        if (DataContext is not HardwareMonitorViewModel vm)
        {
            return;
        }

        if (CpuChartHost.Child is CartesianChart cpuExisting)
        {
            BindingOperations.ClearBinding(cpuExisting, CartesianChart.SeriesProperty);
            BindingOperations.ClearBinding(cpuExisting, CartesianChart.XAxesProperty);
            BindingOperations.ClearBinding(cpuExisting, CartesianChart.YAxesProperty);
        }

        if (MemoryChartHost.Child is CartesianChart memExisting)
        {
            BindingOperations.ClearBinding(memExisting, CartesianChart.SeriesProperty);
            BindingOperations.ClearBinding(memExisting, CartesianChart.XAxesProperty);
            BindingOperations.ClearBinding(memExisting, CartesianChart.YAxesProperty);
        }

        var cpuChart = new CartesianChart { Height = 200 };
        HardwareMonitorChartChrome.ApplyDarkChrome(cpuChart);
        cpuChart.SetBinding(CartesianChart.SeriesProperty, new Binding(nameof(HardwareMonitorViewModel.CpuChartSeries)) { Source = vm });
        cpuChart.SetBinding(CartesianChart.XAxesProperty, new Binding(nameof(HardwareMonitorViewModel.ChartXAxes)) { Source = vm });
        cpuChart.SetBinding(CartesianChart.YAxesProperty, new Binding(nameof(HardwareMonitorViewModel.ChartYAxes)) { Source = vm });
        CpuChartHost.Child = cpuChart;

        var memChart = new CartesianChart { Height = 200 };
        HardwareMonitorChartChrome.ApplyDarkChrome(memChart);
        memChart.SetBinding(CartesianChart.SeriesProperty, new Binding(nameof(HardwareMonitorViewModel.MemoryChartSeries)) { Source = vm });
        memChart.SetBinding(CartesianChart.XAxesProperty, new Binding(nameof(HardwareMonitorViewModel.ChartXAxes)) { Source = vm });
        memChart.SetBinding(CartesianChart.YAxesProperty, new Binding(nameof(HardwareMonitorViewModel.ChartYAxes)) { Source = vm });
        MemoryChartHost.Child = memChart;
    }
}
