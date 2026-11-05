using System.Globalization;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class GpuPowerServiceTests
{
    private const string Uuid0 = "GPU-00000000-0000-0000-0000-000000000000";
    private const string Uuid1 = "GPU-11111111-1111-1111-1111-111111111111";
    private const string DeviceRow = $"0, {Uuid0}, NVIDIA Test GPU, 165.00, 100.00, 165.00, 165.00";

    [Fact]
    public async Task Query_ParsesSameModelDevicesByUuidAndDriverIndex()
    {
        var runner = new FakeRunner($"1, {Uuid1}, \"NVIDIA Test, GPU\", 150.50, 100, 200, 180\n" +
            $"0, {Uuid0}, \"NVIDIA Test, GPU\", 165.00, 100.00, 165.00, 165.00");
        var devices = await CreateService(runner).GetDevicesAsync();

        Assert.Equal(new[] { Uuid0, Uuid1 }, devices.Select(device => device.Uuid));
        Assert.Equal("NVIDIA Test, GPU", devices[0].Name);
        Assert.Equal(150.50m, devices[1].PowerLimit);
        Assert.All(devices, device => Assert.True(device.CanSetPowerLimit));
        var command = Assert.Single(runner.Commands);
        Assert.Equal("nvidia-smi", command.FileName);
        Assert.Equal("--query-gpu=index,uuid,name,power.limit,power.min_limit,power.max_limit,power.default_limit --format=csv,noheader,nounits", command.Arguments);
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("[N/A]")]
    [InlineData("[Not Supported]")]
    public async Task Query_UnsupportedPowerRemainsUnavailable(string unsupported)
    {
        var runner = new FakeRunner($"0, {Uuid0}, NVIDIA GPU, {unsupported}, {unsupported}, {unsupported}, {unsupported}");
        var device = Assert.Single(await CreateService(runner).GetDevicesAsync());

        Assert.False(device.CanSetPowerLimit);
        Assert.Null(device.PowerLimit);
        Assert.Null(device.DefaultPowerLimit);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("garbage")]
    [InlineData("Infinity")]
    public async Task Query_RejectsInvalidNumericData(string value)
    {
        var log = new RecordingLog();
        var runner = new FakeRunner($"0, {Uuid0}, NVIDIA GPU, {value}, 100, 165, 165");
        await Assert.ThrowsAsync<FormatException>(() => new GpuPowerService(runner, log).GetDevicesAsync());
        Assert.Single(log.Errors);
    }

    [Fact]
    public async Task Query_RejectsInvalidRangeAndDuplicateDevices()
    {
        foreach (var output in new[]
        {
            $"0, {Uuid0}, NVIDIA GPU, 165, 200, 100, 165",
            DeviceRow + "\n" + DeviceRow,
            "0, not-a-uuid, NVIDIA GPU, 165, 100, 165, 165",
            "invalid,row"
        })
        {
            await Assert.ThrowsAsync<FormatException>(() => CreateService(new FakeRunner(output)).GetDevicesAsync());
        }
    }

    [Fact]
    public async Task Query_EmptyListIsReported()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(new FakeRunner("")).GetDevicesAsync());
        Assert.Contains("未检测到 NVIDIA", ex.Message);
    }

    [Theory]
    [InlineData(4, "管理员")]
    [InlineData(3, "不支持")]
    [InlineData(-1, "未找到命令")]
    [InlineData(9, "退出码 9")]
    public async Task Query_ReportsFailureAndLogsOnce(int exitCode, string expected)
    {
        var runner = new FakeRunner(DeviceRow)
        {
            Handler = (_, _) => Task.FromResult(new ExternalProcessResult(exitCode, "driver output", "未找到命令: nvidia-smi"))
        };
        var log = new RecordingLog();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new GpuPowerService(runner, log).GetDevicesAsync());

        Assert.Contains(expected, ex.Message);
        Assert.Contains("driver output", ex.Message);
        Assert.Single(log.Errors);
        Assert.Single(runner.Commands);
    }

    [Fact]
    public async Task Set_TargetsUuidAndUsesInvariantDecimalFormat()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var runner = new FakeRunner(DeviceRow + $"\n1, {Uuid1}, NVIDIA Test GPU, 165, 100, 200, 165");
            await CreateService(runner).SetPowerLimitAsync(Uuid1, 150.5m);

            Assert.Equal(2, runner.Commands.Count);
            Assert.Equal($"-i {Uuid1} -pl 150.5", runner.Commands[1].Arguments);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(165)]
    public async Task Set_AcceptsRangeBoundaries(int watts)
    {
        var runner = new FakeRunner(DeviceRow);
        await CreateService(runner).SetPowerLimitAsync(Uuid0, watts);
        Assert.Equal(2, runner.Commands.Count);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(166)]
    [InlineData(-1)]
    public async Task Set_RejectsOutsideFreshDriverRangeWithoutWriting(int watts)
    {
        var runner = new FakeRunner(DeviceRow);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateService(runner).SetPowerLimitAsync(Uuid0, watts));
        Assert.Single(runner.Commands);
    }

    [Fact]
    public async Task Set_RejectsMissingOrUnsupportedDeviceWithoutWriting()
    {
        foreach (var output in new[] { DeviceRow, $"1, {Uuid1}, NVIDIA GPU, N/A, N/A, N/A, N/A" })
        {
            var runner = new FakeRunner(output);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(runner).SetPowerLimitAsync(Uuid1, 150));
            Assert.Single(runner.Commands);
        }
    }

    [Fact]
    public async Task Set_RejectsArbitraryCommandTextWithoutStartingProcess()
    {
        var runner = new FakeRunner(DeviceRow);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(runner).SetPowerLimitAsync("0 & calc.exe", 150));
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task Set_ReportsPermissionFailureWithoutRetrying()
    {
        var runner = new FakeRunner(DeviceRow)
        {
            Handler = (command, _) => Task.FromResult(command.Arguments.StartsWith("-i")
                ? new ExternalProcessResult(4, "Insufficient Permissions", "")
                : new ExternalProcessResult(0, DeviceRow, ""))
        };
        var log = new RecordingLog();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new GpuPowerService(runner, log).SetPowerLimitAsync(Uuid0, 150));
        Assert.Contains("管理员", ex.Message);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Single(log.Errors);
    }

    [Fact]
    public async Task Query_TimesOutAfterTenSecondsWithoutRetrying()
    {
        var runner = new FakeRunner(DeviceRow)
        {
            Handler = async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unreachable");
            }
        };
        var log = new RecordingLog();
        var ex = await Assert.ThrowsAsync<TimeoutException>(() => new GpuPowerService(runner, log).GetDevicesAsync());
        Assert.Contains("10 秒", ex.Message);
        Assert.Single(runner.Commands);
        Assert.Single(log.Errors);
    }

    [Fact]
    public async Task Query_PropagatesUserCancellation()
    {
        var runner = new FakeRunner(DeviceRow)
        {
            Handler = (_, token) => Task.FromCanceled<ExternalProcessResult>(token)
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(runner).GetDevicesAsync(cancellation.Token));
        Assert.Empty(runner.Commands);
    }

    private static GpuPowerService CreateService(FakeRunner runner) => new(runner, new RecordingLog());

    private sealed class FakeRunner(string output) : IExternalProcessRunner
    {
        public List<ExternalProcessCommand> Commands { get; } = new();
        public Func<ExternalProcessCommand, CancellationToken, Task<ExternalProcessResult>>? Handler { get; init; }

        public Task<ExternalProcessResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Handler?.Invoke(command, cancellationToken) ?? Task.FromResult(new ExternalProcessResult(0, output, ""));
        }
    }

    private sealed class RecordingLog : ILogService
    {
        public event EventHandler<string>? LogReceived { add { } remove { } }
        public event EventHandler<LogEntry>? LogEntryReceived { add { } remove { } }
        public List<string> Errors { get; } = new();
        public void Log(string message) { }
        public void Log(string message, GUILogLevel level) { }
        public void LogError(string message, Exception? exception = null) => Errors.Add(message);
    }
}
