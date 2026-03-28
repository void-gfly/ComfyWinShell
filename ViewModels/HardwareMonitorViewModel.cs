using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

/// <summary>
/// 单个 GPU 的显示数据模型
/// </summary>
public partial class GpuDisplayInfo : ObservableObject
{
    public ObservableCollection<DateTimePoint> LoadHistoryPoints { get; } = new();
    public ObservableCollection<DateTimePoint> VramHistoryPoints { get; } = new();
    public ObservableCollection<ISeries> GpuChartSeries { get; }

    public GpuDisplayInfo()
    {
        GpuChartSeries = new ObservableCollection<ISeries>
        {
            HardwareMonitorChartFactory.CreateLineSeries("使用率", LoadHistoryPoints, new SKColor(0x21, 0x96, 0xF3)),
            HardwareMonitorChartFactory.CreateLineSeries("显存占用率", VramHistoryPoints, new SKColor(0xFF, 0x98, 0x00))
        };
    }

    [ObservableProperty]
    private string _name = "Unknown GPU";

    [ObservableProperty]
    private double? _load;

    [ObservableProperty]
    private double? _temperature;

    [ObservableProperty]
    private double? _fanRpm;

    [ObservableProperty]
    private double? _memoryUsed;

    [ObservableProperty]
    private double? _memoryTotal;

    public string LoadText => Load.HasValue ? $"{Load:F1}%" : "N/A";
    public string TemperatureText => Temperature.HasValue ? $"{Temperature:F1}°C" : "N/A";
    public string FanText => FanRpm.HasValue ? $"{FanRpm:F0} RPM" : "N/A";
    public string MemoryText => MemoryUsed.HasValue && MemoryTotal.HasValue
        ? $"{MemoryUsed / 1024:F1} / {MemoryTotal / 1024:F1} GB"
        : "N/A";

    public double MemoryPercent => MemoryUsed.HasValue && MemoryTotal.HasValue && MemoryTotal > 0
        ? MemoryUsed.Value / MemoryTotal.Value * 100
        : 0;

    public void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(LoadText));
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(FanText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(MemoryPercent));
    }

    /// <summary>图表时间窗内「使用率」序列的最大值（百分比）。</summary>
    public string LoadChartPeakText { get; private set; } = PeakChartLabels.Empty;

    /// <summary>图表时间窗内「显存占用率」折算的显存用量峰值（GB）。</summary>
    public string VramChartPeakText { get; private set; } = PeakChartLabels.Empty;

    public void UpdateChartPeakLabels()
    {
        LoadChartPeakText = PeakChartLabels.FormatPercentPeak(LoadHistoryPoints);
        VramChartPeakText = PeakChartLabels.FormatVramPeakGb(VramHistoryPoints, MemoryTotal);
        OnPropertyChanged(nameof(LoadChartPeakText));
        OnPropertyChanged(nameof(VramChartPeakText));
    }
}

public partial class HardwareMonitorViewModel : ViewModelBase, IDisposable
{
    private const int MaxHistoryPoints = (HardwareMonitorChartFactory.WindowMinutes * 60) / HardwareMonitorChartFactory.SampleIntervalSeconds;

    private readonly IHardwareMonitorService _hardwareMonitorService;
    private readonly ILogService _logService;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public ObservableCollection<DateTimePoint> CpuLoadHistoryPoints { get; } = new();
    public ObservableCollection<DateTimePoint> MemoryHistoryPoints { get; } = new();
    public ObservableCollection<DateTimePoint> DiskReadHistoryPoints { get; } = new();
    public ObservableCollection<DateTimePoint> DiskWriteHistoryPoints { get; } = new();

    public ObservableCollection<ISeries> CpuChartSeries { get; }
    public ObservableCollection<ISeries> MemoryChartSeries { get; }
    public ObservableCollection<ISeries> DiskChartSeries { get; }

    public ObservableCollection<ICartesianAxis> ChartXAxes { get; }
    public ObservableCollection<ICartesianAxis> ChartYAxes { get; }
    public ObservableCollection<ICartesianAxis> DiskChartYAxes { get; }

    public HardwareMonitorViewModel(IHardwareMonitorService hardwareMonitorService, ILogService logService)
    {
        _hardwareMonitorService = hardwareMonitorService;
        _logService = logService;

        ChartXAxes = HardwareMonitorChartFactory.CreateTimeXAxes();
        ChartYAxes = HardwareMonitorChartFactory.CreatePercentYAxes();
        DiskChartYAxes = HardwareMonitorChartFactory.CreateSpeedYAxes();

        CpuChartSeries = new ObservableCollection<ISeries>
        {
            HardwareMonitorChartFactory.CreateLineSeries("CPU 使用率", CpuLoadHistoryPoints, new SKColor(0x4C, 0xAF, 0x50))
        };
        MemoryChartSeries = new ObservableCollection<ISeries>
        {
            HardwareMonitorChartFactory.CreateLineSeries("内存占用率", MemoryHistoryPoints, new SKColor(0x9C, 0x27, 0xB0))
        };
        DiskChartSeries = new ObservableCollection<ISeries>
        {
            HardwareMonitorChartFactory.CreateLineSeries("读取", DiskReadHistoryPoints, new SKColor(0x00, 0xBC, 0xD4), " MB/s"),
            HardwareMonitorChartFactory.CreateLineSeries("写入", DiskWriteHistoryPoints, new SKColor(0xFF, 0x57, 0x22), " MB/s")
        };

        RefreshCommand = new RelayCommand(Refresh);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(HardwareMonitorChartFactory.SampleIntervalSeconds)
        };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    [ObservableProperty]
    private string _cpuName = "CPU";

    [ObservableProperty]
    private double? _cpuLoad;

    [ObservableProperty]
    private double? _cpuTemperature;

    [ObservableProperty]
    private double? _cpuFanRpm;

    /// <summary>
    /// 所有 GPU 的显示信息列表
    /// </summary>
    public ObservableCollection<GpuDisplayInfo> Gpus { get; } = new();

    [ObservableProperty]
    private double? _memoryUsed;

    [ObservableProperty]
    private double? _memoryTotal;

    [ObservableProperty]
    private double? _diskReadRate;

    [ObservableProperty]
    private double? _diskWriteRate;

    [ObservableProperty]
    private string _lastUpdateTime = "";

    [ObservableProperty]
    private bool _isMonitoring = true;

    /// <summary>CPU 图表时间窗内使用率峰值。</summary>
    [ObservableProperty]
    private string _cpuChartPeakText = PeakChartLabels.Empty;

    /// <summary>内存图表时间窗内占用峰值（按当前总内存折算为 GB）。</summary>
    [ObservableProperty]
    private string _memoryChartPeakText = PeakChartLabels.Empty;

    [ObservableProperty]
    private string _diskReadChartPeakText = PeakChartLabels.Empty;

    [ObservableProperty]
    private string _diskWriteChartPeakText = PeakChartLabels.Empty;

    public IRelayCommand RefreshCommand { get; }

    public string CpuLoadText => CpuLoad.HasValue ? $"{CpuLoad:F1}%" : "N/A";
    public string CpuTemperatureText => CpuTemperature.HasValue ? $"{CpuTemperature:F1}°C" : "N/A";
    public string CpuFanText => CpuFanRpm.HasValue ? $"{CpuFanRpm:F0} RPM" : "N/A";

    public string MemoryText => MemoryUsed.HasValue && MemoryTotal.HasValue
        ? $"{MemoryUsed / 1024:F1} / {MemoryTotal / 1024:F1} GB"
        : "N/A";

    public double MemoryPercent => MemoryUsed.HasValue && MemoryTotal.HasValue && MemoryTotal > 0
        ? MemoryUsed.Value / MemoryTotal.Value * 100
        : 0;

    public string DiskReadText => DiskReadRate.HasValue ? $"{DiskReadRate:F1} MB/s" : "N/A";
    public string DiskWriteText => DiskWriteRate.HasValue ? $"{DiskWriteRate:F1} MB/s" : "N/A";

    partial void OnIsMonitoringChanged(bool value)
    {
        if (value)
        {
            _timer.Start();
            Refresh();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void Refresh()
    {
        try
        {
            var snapshot = _hardwareMonitorService.GetSnapshot();

            CpuName = snapshot.CpuName;
            CpuLoad = snapshot.CpuLoadPercent;
            CpuTemperature = snapshot.CpuTemperatureC;
            CpuFanRpm = snapshot.CpuFanRpm;

            UpdateGpuList(snapshot.Gpus);

            MemoryUsed = snapshot.MemoryUsedMb;
            MemoryTotal = snapshot.MemoryTotalMb;

            DiskReadRate = snapshot.DiskReadRateMb;
            DiskWriteRate = snapshot.DiskWriteRateMb;

            LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");

            OnPropertyChanged(nameof(CpuLoadText));
            OnPropertyChanged(nameof(CpuTemperatureText));
            OnPropertyChanged(nameof(CpuFanText));
            OnPropertyChanged(nameof(MemoryText));
            OnPropertyChanged(nameof(MemoryPercent));
            OnPropertyChanged(nameof(DiskReadText));
            OnPropertyChanged(nameof(DiskWriteText));

            AppendHistoryAndTrim(snapshot);
        }
        catch (Exception ex)
        {
            _logService.LogError("刷新硬件监控数据失败", ex);
        }
    }

    private void AppendHistoryAndTrim(HwInfoSnapshot snapshot)
    {
        var now = DateTime.Now;

        AppendPoint(CpuLoadHistoryPoints, snapshot.CpuLoadPercent, now);

        var memPct = snapshot.MemoryTotalMb is > 0 && snapshot.MemoryUsedMb is { } used
            ? used / snapshot.MemoryTotalMb.Value * 100
            : (double?)null;
        AppendPoint(MemoryHistoryPoints, memPct, now);

        AppendPoint(DiskReadHistoryPoints, snapshot.DiskReadRateMb, now);
        AppendPoint(DiskWriteHistoryPoints, snapshot.DiskWriteRateMb, now);

        for (var i = 0; i < Gpus.Count && i < snapshot.Gpus.Count; i++)
        {
            var g = snapshot.Gpus[i];
            var display = Gpus[i];
            AppendPoint(display.LoadHistoryPoints, g.LoadPercent, now);

            var vramPct = g.MemoryTotalMb is > 0 && g.MemoryUsedMb is { } mu
                ? mu / g.MemoryTotalMb.Value * 100
                : (double?)null;
            AppendPoint(display.VramHistoryPoints, vramPct, now);
        }

        TrimWindow(CpuLoadHistoryPoints, now);
        TrimWindow(MemoryHistoryPoints, now);
        TrimWindow(DiskReadHistoryPoints, now);
        TrimWindow(DiskWriteHistoryPoints, now);
        foreach (var gpu in Gpus)
        {
            TrimWindow(gpu.LoadHistoryPoints, now);
            TrimWindow(gpu.VramHistoryPoints, now);
        }

        UpdateAxisLimits(now);
        UpdateChartPeakLabels();
    }

    private void UpdateChartPeakLabels()
    {
        CpuChartPeakText = PeakChartLabels.FormatPercentPeak(CpuLoadHistoryPoints);
        MemoryChartPeakText = PeakChartLabels.FormatMemoryPeakGb(MemoryHistoryPoints, MemoryTotal);
        DiskReadChartPeakText = PeakChartLabels.FormatSpeedPeak(DiskReadHistoryPoints);
        DiskWriteChartPeakText = PeakChartLabels.FormatSpeedPeak(DiskWriteHistoryPoints);
        foreach (var gpu in Gpus)
        {
            gpu.UpdateChartPeakLabels();
        }
    }

    private static void AppendPoint(ObservableCollection<DateTimePoint> points, double? value, DateTime now)
    {
        if (!value.HasValue)
        {
            return;
        }

        points.Add(new DateTimePoint(now, value.Value));
    }

    private static void TrimWindow(ObservableCollection<DateTimePoint> points, DateTime now)
    {
        var cutoff = now - HardwareMonitorChartFactory.WindowDuration;
        while (points.Count > 0 && points[0].DateTime < cutoff)
        {
            points.RemoveAt(0);
        }

        while (points.Count > MaxHistoryPoints)
        {
            points.RemoveAt(0);
        }
    }

    private void UpdateAxisLimits(DateTime now)
    {
        if (ChartXAxes.Count == 0 || ChartXAxes[0] is not Axis xAxis)
        {
            return;
        }

        xAxis.MinLimit = now.Subtract(HardwareMonitorChartFactory.WindowDuration).Ticks;
        xAxis.MaxLimit = now.Ticks;
    }

    private void UpdateGpuList(List<GpuInfoSnapshot> gpuSnapshots)
    {
        if (Gpus.Count != gpuSnapshots.Count)
        {
            Gpus.Clear();
            foreach (var gpu in gpuSnapshots)
            {
                var display = new GpuDisplayInfo
                {
                    Name = gpu.Name,
                    Load = gpu.LoadPercent,
                    Temperature = gpu.TemperatureC,
                    FanRpm = gpu.FanRpm,
                    MemoryUsed = gpu.MemoryUsedMb,
                    MemoryTotal = gpu.MemoryTotalMb
                };
                Gpus.Add(display);
            }

            return;
        }

        for (var i = 0; i < gpuSnapshots.Count; i++)
        {
            var gpu = gpuSnapshots[i];
            var display = Gpus[i];
            display.Name = gpu.Name;
            display.Load = gpu.LoadPercent;
            display.Temperature = gpu.TemperatureC;
            display.FanRpm = gpu.FanRpm;
            display.MemoryUsed = gpu.MemoryUsedMb;
            display.MemoryTotal = gpu.MemoryTotalMb;
            display.NotifyAllChanged();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _timer.Stop();
        _disposed = true;
    }
}
