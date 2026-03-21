using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LiveChartsCore.SkiaSharpView.WPF;
using WpfDesktop.ViewModels;

namespace WpfDesktop.Views;

/// <summary>
/// 在代码中创建 CartesianChart，避免 XAML 编译器（MarkupCompile）无法解析 LiveChartsCore 程序集导致的 MC1000。
/// </summary>
public sealed class GpuChartHostControl : UserControl
{
    private readonly Border _chartRoot = new() { Height = 200 };

    public GpuChartHostControl()
    {
        Content = _chartRoot;
        Loaded += (_, _) => WireChart();
        DataContextChanged += (_, _) => WireChart();
    }

    private void WireChart()
    {
        if (DataContext is not GpuDisplayInfo gpu)
        {
            return;
        }

        var view = FindAncestorHardwareMonitorView(this);
        if (view?.DataContext is not HardwareMonitorViewModel vm)
        {
            return;
        }

        if (_chartRoot.Child is CartesianChart existing)
        {
            BindingOperations.ClearBinding(existing, CartesianChart.SeriesProperty);
            BindingOperations.ClearBinding(existing, CartesianChart.XAxesProperty);
            BindingOperations.ClearBinding(existing, CartesianChart.YAxesProperty);
        }

        var chart = new CartesianChart { Height = 200 };
        HardwareMonitorChartChrome.ApplyDarkChrome(chart);

        chart.SetBinding(CartesianChart.SeriesProperty, new Binding(nameof(GpuDisplayInfo.GpuChartSeries)) { Source = gpu });
        chart.SetBinding(CartesianChart.XAxesProperty, new Binding(nameof(HardwareMonitorViewModel.ChartXAxes)) { Source = vm });
        chart.SetBinding(CartesianChart.YAxesProperty, new Binding(nameof(HardwareMonitorViewModel.ChartYAxes)) { Source = vm });

        _chartRoot.Child = chart;
    }

    private static HardwareMonitorView? FindAncestorHardwareMonitorView(DependencyObject? child)
    {
        while (child != null)
        {
            if (child is HardwareMonitorView v)
            {
                return v;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
