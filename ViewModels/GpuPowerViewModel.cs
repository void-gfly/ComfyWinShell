using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class GpuPowerViewModel : ViewModelBase
{
    private readonly IGpuPowerService _powerService;
    private bool _syncingInput;

    public GpuPowerViewModel(IGpuPowerService powerService)
    {
        _powerService = powerService;
    }

    public ObservableCollection<GpuPowerDevice> Devices { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanSelectDevice))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(ApplyCommand), nameof(RestoreDefaultCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CurrentLimitText), nameof(DefaultLimitText), nameof(RangeText),
        nameof(ValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(RestoreDefaultCommand))]
    private GpuPowerDevice? _selectedDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private string _powerLimitText = string.Empty;

    [ObservableProperty]
    private double _sliderWatts;

    [ObservableProperty]
    private string _statusText = "正在读取 GPU 功耗信息…";

    public bool CanSelectDevice => !IsBusy;
    public bool CanEdit => !IsBusy && SelectedDevice?.CanSetPowerLimit == true;
    public double MinimumWatts => (double)(SelectedDevice?.MinPowerLimit ?? 0);
    public double MaximumWatts => (double)(SelectedDevice?.MaxPowerLimit ?? 0);
    public string CurrentLimitText => FormatWatts(SelectedDevice?.PowerLimit);
    public string DefaultLimitText => FormatWatts(SelectedDevice?.DefaultPowerLimit);
    public string RangeText => SelectedDevice?.CanSetPowerLimit == true
        ? $"{FormatWatts(SelectedDevice.MinPowerLimit)} – {FormatWatts(SelectedDevice.MaxPowerLimit)}"
        : "不支持功耗调节或未提供功耗范围";

    public string ValidationMessage => SelectedDevice?.CanSetPowerLimit != true
        ? string.Empty
        : TryGetWatts(out _) ? string.Empty : $"请输入 {RangeText} 范围内的数值（小数使用 .）。";

    partial void OnSelectedDeviceChanged(GpuPowerDevice? value)
    {
        // 先更新范围，再设置目标值，避免切换显卡时 Slider 按旧范围截断新值。
        _syncingInput = true;
        try
        {
            OnPropertyChanged(nameof(MinimumWatts));
            OnPropertyChanged(nameof(MaximumWatts));
        }
        finally
        {
            _syncingInput = false;
        }
        ResetPowerInput();
        StatusText = value == null ? "未检测到 NVIDIA 显卡。"
            : value.CanSetPowerLimit ? "调整后点击“应用”生效。" : RangeText;
    }

    partial void OnPowerLimitTextChanged(string value)
    {
        if (_syncingInput || !TryGetWatts(out var watts)) return;
        _syncingInput = true;
        try
        {
            SliderWatts = (double)watts;
        }
        finally
        {
            _syncingInput = false;
        }
    }

    partial void OnSliderWattsChanged(double value)
    {
        if (_syncingInput) return;
        _syncingInput = true;
        try
        {
            PowerLimitText = value.ToString("0.################", CultureInfo.InvariantCulture);
        }
        finally
        {
            _syncingInput = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "正在读取 GPU 功耗信息…";
        try
        {
            await ReloadDevicesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "查询已取消。";
        }
        catch (Exception ex)
        {
            Devices.Clear();
            SelectedDevice = null;
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync(CancellationToken cancellationToken) =>
        TryGetWatts(out var watts) && !IsBusy ? SetPowerLimitAsync(watts, cancellationToken) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanRestoreDefault))]
    private Task RestoreDefaultAsync(CancellationToken cancellationToken) =>
        CanRestoreDefault() && SelectedDevice?.DefaultPowerLimit is decimal watts
            ? SetPowerLimitAsync(watts, cancellationToken) : Task.CompletedTask;

    private async Task SetPowerLimitAsync(decimal watts, CancellationToken cancellationToken)
    {
        var device = SelectedDevice;
        if (device == null) return;
        IsBusy = true;
        StatusText = $"正在设置功耗上限为 {FormatWatts(watts)}…";
        var applied = false;
        try
        {
            await _powerService.SetPowerLimitAsync(device.Uuid, watts, cancellationToken);
            applied = true;
            await ReloadDevicesAsync(cancellationToken, device.Uuid);
            StatusText = $"功耗上限已设置为 {FormatWatts(watts)}，当前上限：{CurrentLimitText}。";
        }
        catch (OperationCanceledException)
        {
            StatusText = applied ? "设置成功，刷新已取消；请刷新确认当前功耗上限。"
                : "操作已取消，请刷新确认当前功耗上限。";
        }
        catch (Exception ex)
        {
            StatusText = applied ? $"设置成功，但刷新失败：{ex.Message}" : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadDevicesAsync(CancellationToken cancellationToken, string? requiredUuid = null)
    {
        var uuid = SelectedDevice?.Uuid;
        var devices = await _powerService.GetDevicesAsync(cancellationToken);
        if (requiredUuid != null && !devices.Any(device => device.Uuid == requiredUuid))
        {
            throw new InvalidOperationException("所设置的显卡已不可用，请刷新设备列表。");
        }
        Devices.Clear();
        foreach (var device in devices) Devices.Add(device);
        SelectedDevice = devices.FirstOrDefault(device => device.Uuid == uuid)
            ?? devices.FirstOrDefault(device => device.Index == 0) ?? devices.FirstOrDefault();
        // 即使刷新结果与旧记录相等，也要舍弃未提交的输入，重新显示驱动值。
        ResetPowerInput();
        StatusText = SelectedDevice == null ? "未检测到 NVIDIA 显卡。"
            : SelectedDevice.CanSetPowerLimit ? "调整后点击“应用”生效。" : RangeText;
    }

    private void ResetPowerInput()
    {
        _syncingInput = true;
        try
        {
            PowerLimitText = SelectedDevice?.PowerLimit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            SliderWatts = SelectedDevice?.CanSetPowerLimit == true && SelectedDevice.PowerLimit is decimal watts
                ? (double)watts : 0;
        }
        finally
        {
            _syncingInput = false;
        }
    }

    private bool CanRefresh() => !IsBusy;
    private bool CanApply() => CanEdit && TryGetWatts(out _);
    private bool CanRestoreDefault() => CanEdit && SelectedDevice?.DefaultPowerLimit is decimal watts &&
        watts >= SelectedDevice.MinPowerLimit && watts <= SelectedDevice.MaxPowerLimit;

    private bool TryGetWatts(out decimal watts) =>
        decimal.TryParse(PowerLimitText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out watts) && SelectedDevice?.CanSetPowerLimit == true &&
        watts >= SelectedDevice.MinPowerLimit && watts <= SelectedDevice.MaxPowerLimit;

    private static string FormatWatts(decimal? watts) =>
        watts.HasValue ? $"{watts.Value.ToString("0.##", CultureInfo.InvariantCulture)} W" : "不支持";

    public void CancelPendingOperations()
    {
        RefreshCommand.Cancel();
        ApplyCommand.Cancel();
        RestoreDefaultCommand.Cancel();
    }
}
