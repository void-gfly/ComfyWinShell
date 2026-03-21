using System.Windows.Media;
using LiveChartsCore.SkiaSharpView.WPF;

namespace WpfDesktop.Views;

/// <summary>
/// 与 <c>Resources/Styles/Colors.xaml</c> 暗黑主题大致一致的图表背景。
/// 注意：不得在此引用 <c>LiveChartsCore</c> 中的 <c>Theme</c>/<c>Paint</c> 等类型——
/// WPF MarkupCompile 的 <c>*_wpftmp</c> 临时工程不会为这类引用补全程序集，会导致 CS0012。
/// 图例/工具提示配色使用控件默认样式即可。
/// </summary>
internal static class HardwareMonitorChartChrome
{
    public static void ApplyDarkChrome(CartesianChart chart)
    {
        chart.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
    }
}
