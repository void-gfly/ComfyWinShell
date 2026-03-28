using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;

namespace WpfDesktop.ViewModels;

/// <summary>
/// 硬件监控折线图：当前时间窗内序列最大值的展示文案。
/// </summary>
internal static class PeakChartLabels
{
    public const string Empty = "峰值: —";

    public static string FormatPercentPeak(ObservableCollection<DateTimePoint> points)
    {
        var max = MaxHistoryValue(points);
        return max.HasValue ? $"峰值: {max.Value:F1}%" : Empty;
    }

    /// <summary>
    /// 历史序列存的是内存占用率（%），用当前总内存折算为「峰值占用 GB」。
    /// </summary>
    public static string FormatMemoryPeakGb(ObservableCollection<DateTimePoint> points, double? totalMb)
    {
        var maxPct = MaxHistoryValue(points);
        if (!maxPct.HasValue)
        {
            return Empty;
        }

        if (totalMb is > 0)
        {
            var peakGb = maxPct.Value / 100.0 * totalMb.Value / 1024.0;
            return $"峰值: {peakGb:F1} GB";
        }

        return $"峰值: {maxPct.Value:F1}%";
    }

    public static string FormatVramPeakGb(ObservableCollection<DateTimePoint> points, double? totalMb)
    {
        return FormatMemoryPeakGb(points, totalMb);
    }

    public static string FormatSpeedPeak(ObservableCollection<DateTimePoint> points)
    {
        var max = MaxHistoryValue(points);
        return max.HasValue ? $"峰值: {max.Value:F1} MB/s" : Empty;
    }

    private static double? MaxHistoryValue(ObservableCollection<DateTimePoint> points)
    {
        double? max = null;
        foreach (var p in points)
        {
            if (!p.Value.HasValue)
            {
                continue;
            }

            var v = p.Value.Value;
            if (max == null || v > max)
            {
                max = v;
            }
        }

        return max;
    }
}
