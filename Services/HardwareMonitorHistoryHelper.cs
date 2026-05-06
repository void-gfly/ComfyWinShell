using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;

namespace WpfDesktop.Services;

public static class HardwareMonitorHistoryHelper
{
    public static void BuildCumulativePoints(
        IEnumerable<DateTimePoint> sourcePoints,
        ObservableCollection<DateTimePoint> targetPoints,
        double sampleIntervalSeconds,
        double pageSizeBytes)
    {
        targetPoints.Clear();

        var total = 0d;
        foreach (var point in sourcePoints)
        {
            if (!point.Value.HasValue)
            {
                continue;
            }

            total += point.Value.Value * sampleIntervalSeconds * pageSizeBytes / (1024.0 * 1024.0);
            targetPoints.Add(new DateTimePoint(point.DateTime, total));
        }
    }
}
