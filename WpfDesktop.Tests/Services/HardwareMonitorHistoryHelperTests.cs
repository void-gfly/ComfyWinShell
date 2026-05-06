using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class HardwareMonitorHistoryHelperTests
{
    [Fact]
    public void BuildCumulativePoints_MultipliesEachSampleByIntervalAndAccumulates()
    {
        var sourcePoints = new[]
        {
            new DateTimePoint(new DateTime(2026, 5, 6, 12, 0, 0), 1),
            new DateTimePoint(new DateTime(2026, 5, 6, 12, 0, 5), 2),
            new DateTimePoint(new DateTime(2026, 5, 6, 12, 0, 10), 3)
        };
        var targetPoints = new ObservableCollection<DateTimePoint>();

        HardwareMonitorHistoryHelper.BuildCumulativePoints(sourcePoints, targetPoints, 5, 4096);

        Assert.Equal(3, targetPoints.Count);
        Assert.InRange(targetPoints[0].Value!.Value, 0.0195, 0.0196);
        Assert.InRange(targetPoints[1].Value!.Value, 0.0585, 0.0586);
        Assert.InRange(targetPoints[2].Value!.Value, 0.1171, 0.1172);
    }
}
