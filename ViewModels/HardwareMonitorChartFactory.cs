using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace WpfDesktop.ViewModels;

internal static class HardwareMonitorChartFactory
{
    public const int SampleIntervalSeconds = 5;
    public const int WindowMinutes = 15;
    public static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(WindowMinutes);

    // 与 Resources/Styles/Colors.xaml（SecondaryText / Border）一致
    private static readonly SKColor AxisLabelColor = new(0xA0, 0xA0, 0xA0);
    private static readonly SKColor AxisSeparatorColor = new(0x2A, 0x2A, 0x2A);
    private static readonly SKColor AxisTickColor = new(0xA0, 0xA0, 0xA0);

    public static LineSeries<DateTimePoint> CreateLineSeries(
        string name,
        ObservableCollection<DateTimePoint> values,
        SKColor stroke)
    {
        return new LineSeries<DateTimePoint>
        {
            Name = name,
            Values = values,
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(stroke) { StrokeThickness = 2 },
            YToolTipLabelFormatter = p => $"{p.Coordinate.PrimaryValue:F1}%"
        };
    }

    public static ObservableCollection<ICartesianAxis> CreateTimeXAxes()
    {
        return new ObservableCollection<ICartesianAxis>
        {
            new Axis
            {
                // rc6.1 无 ShowLabels；用空 Labeler + 极小字号避免横轴显示时间文字
                Labeler = _ => string.Empty,
                TextSize = 0.01f,
                UnitWidth = TimeSpan.FromMinutes(1).Ticks,
                SeparatorsPaint = new SolidColorPaint(AxisSeparatorColor) { StrokeThickness = 1 },
                SubseparatorsPaint = new SolidColorPaint(AxisSeparatorColor.WithAlpha(90)) { StrokeThickness = 0.75f },
                TicksPaint = new SolidColorPaint(AxisTickColor) { StrokeThickness = 1 }
            }
        };
    }

    public static ObservableCollection<ICartesianAxis> CreatePercentYAxes()
    {
        return new ObservableCollection<ICartesianAxis>
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                Name = "%",
                NamePaint = new SolidColorPaint(AxisLabelColor),
                LabelsPaint = new SolidColorPaint(AxisLabelColor),
                SeparatorsPaint = new SolidColorPaint(AxisSeparatorColor) { StrokeThickness = 1 },
                SubseparatorsPaint = new SolidColorPaint(AxisSeparatorColor.WithAlpha(90)) { StrokeThickness = 0.75f },
                TicksPaint = new SolidColorPaint(AxisTickColor) { StrokeThickness = 1 }
            }
        };
    }
}
