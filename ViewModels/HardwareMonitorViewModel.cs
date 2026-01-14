using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

/// <summary>
/// 单个 GPU 的显示数据模型
/// </summary>
public partial class GpuDisplayInfo : ObservableObject
{
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
}

public partial class HardwareMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly IHardwareMonitorService _hardwareMonitorService;
    private readonly ILogService _logService;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public HardwareMonitorViewModel(IHardwareMonitorService hardwareMonitorService, ILogService logService)
    {
        _hardwareMonitorService = hardwareMonitorService;
        _logService = logService;

        RefreshCommand = new RelayCommand(Refresh);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
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
    private string _lastUpdateTime = "";

    [ObservableProperty]
    private bool _isMonitoring = true;

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

            // 更新 GPU 列表
            UpdateGpuList(snapshot.Gpus);

            MemoryUsed = snapshot.MemoryUsedMb;
            MemoryTotal = snapshot.MemoryTotalMb;

            LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");

            OnPropertyChanged(nameof(CpuLoadText));
            OnPropertyChanged(nameof(CpuTemperatureText));
            OnPropertyChanged(nameof(CpuFanText));
            OnPropertyChanged(nameof(MemoryText));
            OnPropertyChanged(nameof(MemoryPercent));
        }
        catch (Exception ex)
        {
            _logService.LogError("刷新硬件监控数据失败", ex);
        }
    }

    private void UpdateGpuList(List<GpuInfoSnapshot> gpuSnapshots)
    {
        // 如果 GPU 数量变化，重建列表
        if (Gpus.Count != gpuSnapshots.Count)
        {
            Gpus.Clear();
            foreach (var gpu in gpuSnapshots)
            {
                Gpus.Add(new GpuDisplayInfo
                {
                    Name = gpu.Name,
                    Load = gpu.LoadPercent,
                    Temperature = gpu.TemperatureC,
                    FanRpm = gpu.FanRpm,
                    MemoryUsed = gpu.MemoryUsedMb,
                    MemoryTotal = gpu.MemoryTotalMb
                });
            }
        }
        else
        {
            // 更新现有项
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
    }

    public void Dispose()
    {
        if (_disposed) return;
        _timer.Stop();
        _disposed = true;
    }
}
