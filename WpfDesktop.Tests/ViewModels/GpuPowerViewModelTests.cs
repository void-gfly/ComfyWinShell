using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;
using WpfDesktop.ViewModels;
using Xunit;

namespace WpfDesktop.Tests.ViewModels;

public sealed class GpuPowerViewModelTests
{
    private static readonly GpuPowerDevice Device0 = new(0, "GPU-00000000-0000-0000-0000-000000000000", "NVIDIA GPU", 150, 100, 200, 165);
    private static readonly GpuPowerDevice Device1 = new(1, "GPU-11111111-1111-1111-1111-111111111111", "NVIDIA GPU", 400, 200, 500, 450);

    [Fact]
    public async Task Refresh_SelectsDriverIndexZeroAndResetsInputOnDeviceChange()
    {
        var service = new FakeService { Devices = new[] { Device1, Device0 } };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(Device0, vm.SelectedDevice);
        Assert.Equal("150", vm.PowerLimitText);
        Assert.Equal(150d, vm.SliderWatts);
        vm.SelectedDevice = Device1;
        Assert.Equal("400", vm.PowerLimitText);
        Assert.Equal(400d, vm.SliderWatts);
        Assert.Equal(200d, vm.MinimumWatts);
        Assert.Equal(500d, vm.MaximumWatts);
        Assert.Equal("450 W", vm.DefaultLimitText);
        Assert.Empty(service.Writes);
    }

    [Fact]
    public async Task InputAndSliderStaySynchronizedWithoutExecutingCommand()
    {
        var service = new FakeService();
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.PowerLimitText = "155.50";
        Assert.Equal(155.5, vm.SliderWatts);
        Assert.Equal("155.50", vm.PowerLimitText);
        vm.SliderWatts = 160;
        Assert.Equal("160", vm.PowerLimitText);
        Assert.True(vm.ApplyCommand.CanExecute(null));
        Assert.Empty(service.Writes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("99")]
    [InlineData("201")]
    [InlineData("-1")]
    [InlineData("150,5")]
    [InlineData("150 & calc.exe")]
    public async Task InvalidInputDisablesApplyAndDoesNotWrite(string text)
    {
        var service = new FakeService();
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.PowerLimitText = text;

        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.NotEmpty(vm.ValidationMessage);
        await vm.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(service.Writes);
    }

    [Fact]
    public async Task ApplyWritesSelectedUuidAndReadsActualDriverValue()
    {
        var service = new FakeService { Devices = new[] { Device0, Device1 } };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedDevice = Device1;
        vm.PowerLimitText = "350.5";
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal((Device1.Uuid, 350.5m), Assert.Single(service.Writes));
        Assert.Equal("350.5 W", vm.CurrentLimitText);
        Assert.Equal("350.5", vm.PowerLimitText);
        Assert.Equal(2, service.QueryCount);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task RestoreDefaultSubmitsDefaultDirectly()
    {
        var service = new FakeService();
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.PowerLimitText = "180";
        await vm.RestoreDefaultCommand.ExecuteAsync(null);

        Assert.Equal((Device0.Uuid, 165m), Assert.Single(service.Writes));
        Assert.Equal("165 W", vm.CurrentLimitText);
    }

    [Fact]
    public async Task RefreshPreservesSelectionAndDiscardsUnsubmittedInput()
    {
        var service = new FakeService { Devices = new[] { Device0, Device1 } };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedDevice = Device1;
        vm.PowerLimitText = "350";
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(Device1.Uuid, vm.SelectedDevice?.Uuid);
        Assert.Equal("400", vm.PowerLimitText);
        Assert.Equal(400d, vm.SliderWatts);
        Assert.Empty(service.Writes);
    }

    [Fact]
    public async Task BusyDisablesAllCommandsAndEditingAcrossOperations()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeService { BeforeWrite = token => completion.Task.WaitAsync(token) };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        var apply = vm.ApplyCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.CanEdit);
        Assert.False(vm.CanSelectDevice);
        Assert.False(vm.RefreshCommand.CanExecute(null));
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.RestoreDefaultCommand.CanExecute(null));
        await vm.RestoreDefaultCommand.ExecuteAsync(null);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Single(service.Writes);
        completion.SetResult();
        await apply;
        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task ApplyReportsSuccessfulWriteSeparatelyFromFailedRefresh()
    {
        var service = new FakeService();
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        service.FailQuery = true;
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Single(service.Writes);
        Assert.Contains("设置成功，但刷新失败", vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ApplyDoesNotReportAnotherDeviceAsTheWrittenDevice()
    {
        var service = new FakeService { DevicesAfterWrite = new[] { Device1 } };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyCommand.ExecuteAsync(null);
        Assert.Contains("设置成功，但刷新失败", vm.StatusText);
        Assert.Contains("所设置的显卡已不可用", vm.StatusText);
    }

    [Theory]
    [InlineData("权限不足，请以管理员身份运行程序后重试。")]
    [InlineData("nvidia-smi 执行超时（10 秒）。")]
    public async Task FailedWriteShowsErrorWithoutSuccessOrRefresh(string message)
    {
        var service = new FakeService { WriteError = message };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.StatusText);
        Assert.Equal(1, service.QueryCount);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task UnsupportedDeviceDisablesEditingAndRestore()
    {
        var service = new FakeService { Devices = new[] { Device0 with { MinPowerLimit = null } } };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.CanEdit);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.RestoreDefaultCommand.CanExecute(null));
        Assert.Contains("不支持", vm.RangeText);
    }

    [Fact]
    public async Task MissingDefaultDisablesOnlyRestore()
    {
        var service = new FakeService { Devices = new[] { Device0 with { DefaultPowerLimit = null } } };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.CanEdit);
        Assert.True(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.RestoreDefaultCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedRefreshClearsStaleDeviceAndAllowsRetry()
    {
        var service = new FakeService();
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        service.FailQuery = true;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(vm.Devices);
        Assert.Null(vm.SelectedDevice);
        Assert.False(vm.CanEdit);
        Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.Contains("query failed", vm.StatusText);
    }

    [Fact]
    public async Task ClosingCancelsPendingWriteAndReopeningUsesFreshViewModel()
    {
        var service = new FakeService
        {
            BeforeWrite = token => Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        var vm = new GpuPowerViewModel(service);
        await vm.RefreshCommand.ExecuteAsync(null);
        var apply = vm.ApplyCommand.ExecuteAsync(null);
        vm.CancelPendingOperations();
        await apply;

        Assert.False(vm.IsBusy);
        Assert.Contains("取消", vm.StatusText);
        var reopened = new GpuPowerViewModel(service);
        await reopened.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(Device0, reopened.SelectedDevice);
    }

    private sealed class FakeService : IGpuPowerService
    {
        public IReadOnlyList<GpuPowerDevice> Devices { get; set; } = new[] { Device0 };
        public IReadOnlyList<GpuPowerDevice>? DevicesAfterWrite { get; init; }
        public List<(string Uuid, decimal Watts)> Writes { get; } = new();
        public int QueryCount { get; private set; }
        public bool FailQuery { get; set; }
        public string? WriteError { get; init; }
        public Func<CancellationToken, Task>? BeforeWrite { get; init; }

        public Task<IReadOnlyList<GpuPowerDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            QueryCount++;
            if (FailQuery) throw new InvalidOperationException("query failed");
            return Task.FromResult(Devices);
        }

        public async Task SetPowerLimitAsync(string uuid, decimal watts, CancellationToken cancellationToken = default)
        {
            Writes.Add((uuid, watts));
            if (BeforeWrite != null) await BeforeWrite(cancellationToken);
            if (WriteError != null) throw new InvalidOperationException(WriteError);
            Devices = DevicesAfterWrite ?? Devices.Select(device => device.Uuid == uuid ? device with { PowerLimit = watts } : device).ToArray();
        }
    }
}
