using System.Globalization;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 使用 nvidia-smi 查询和设置物理 GPU 的功耗上限。
/// </summary>
public sealed class GpuPowerService : IGpuPowerService
{
    private const string QueryArguments = "--query-gpu=index,uuid,name,power.limit,power.min_limit,power.max_limit,power.default_limit --format=csv,noheader,nounits";
    private readonly IExternalProcessRunner _processRunner;
    private readonly ILogService _logService;

    public GpuPowerService(IExternalProcessRunner processRunner, ILogService logService)
    {
        _processRunner = processRunner;
        _logService = logService;
    }

    public async Task<IReadOnlyList<GpuPowerDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await QueryDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.LogError("读取 GPU 功耗信息失败", ex);
            throw;
        }
    }

    private async Task<IReadOnlyList<GpuPowerDevice>> QueryDevicesAsync(CancellationToken cancellationToken)
    {
        var result = await RunCommandAsync(QueryArguments, cancellationToken).ConfigureAwait(false);
        try
        {
            using var reader = new StringReader(result.StandardOutput);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(",");
            var devices = new List<GpuPowerDevice>();
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null || fields.Length != 7 ||
                    !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                    !IsValidUuid(fields[1]) || string.IsNullOrWhiteSpace(fields[2]))
                {
                    throw new FormatException("nvidia-smi 返回了无效的设备数据。");
                }

                var device = new GpuPowerDevice(index, fields[1], fields[2],
                    ParsePower(fields[3]), ParsePower(fields[4]), ParsePower(fields[5]), ParsePower(fields[6]));
                if (device.MinPowerLimit > device.MaxPowerLimit ||
                    devices.Any(existing => existing.Uuid == device.Uuid || existing.Index == device.Index))
                {
                    throw new FormatException("nvidia-smi 返回了无效的功耗范围或重复设备。");
                }

                devices.Add(device);
            }

            if (devices.Count == 0)
            {
                throw new InvalidOperationException("未检测到 NVIDIA 显卡。");
            }

            return devices.OrderBy(device => device.Index).ToArray();
        }
        catch (MalformedLineException ex)
        {
            throw new FormatException("nvidia-smi 返回了无效的 CSV 数据。", ex);
        }
    }

    public async Task SetPowerLimitAsync(string uuid, decimal watts, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsValidUuid(uuid))
            {
                throw new ArgumentException("GPU UUID 无效。", nameof(uuid));
            }

            var devices = await QueryDevicesAsync(cancellationToken).ConfigureAwait(false);
            var device = devices.SingleOrDefault(item => item.Uuid == uuid)
                ?? throw new InvalidOperationException("所选显卡已不可用，请刷新设备列表。");
            if (!device.CanSetPowerLimit)
            {
                throw new InvalidOperationException("所选显卡不支持功耗调节，或驱动未提供功耗范围。");
            }

            if (watts < device.MinPowerLimit || watts > device.MaxPowerLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(watts),
                    $"功耗必须在 {device.MinPowerLimit}–{device.MaxPowerLimit} W 范围内。");
            }

            await RunCommandAsync($"-i {uuid} -pl {watts.ToString(CultureInfo.InvariantCulture)}", cancellationToken).ConfigureAwait(false);
            _logService.Log($"GPU {device.Index} ({device.Name}) 功耗上限已设置为 {watts} W。", GUILogLevel.Info);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.LogError("设置 GPU 功耗失败", ex);
            throw;
        }
    }

    private async Task<ExternalProcessResult> RunCommandAsync(string arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var result = await _processRunner.RunAsync(new ExternalProcessCommand
            {
                FileName = "nvidia-smi",
                Arguments = arguments,
                WorkingDirectory = AppContext.BaseDirectory
            }, timeout.Token).ConfigureAwait(false);

            if (!result.Success)
            {
                var detail = string.Join(Environment.NewLine,
                    new[] { result.StandardOutput.Trim(), result.StandardError.Trim() }.Where(text => text.Length > 0));
                var message = result.ExitCode == 4
                    ? "权限不足，请以管理员身份运行程序后重试。"
                    : result.ExitCode == 3
                        ? "所选显卡不支持此功耗操作。"
                        : $"nvidia-smi 执行失败（退出码 {result.ExitCode}）。";
                throw new InvalidOperationException($"{message}{Environment.NewLine}{detail}".Trim());
            }

            return result;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException("nvidia-smi 执行超时（10 秒）。请刷新确认当前功耗上限。", ex);
        }
    }

    private static bool IsValidUuid(string uuid) =>
        uuid.StartsWith("GPU-", StringComparison.Ordinal) && Guid.TryParseExact(uuid.AsSpan(4), "D", out _);

    private static decimal? ParsePower(string value)
    {
        if (value is "N/A" or "[N/A]" or "[Not Supported]")
        {
            return null;
        }

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var watts) || watts <= 0)
        {
            throw new FormatException($"nvidia-smi 返回了无效的功耗值：{value}");
        }

        return watts;
    }
}
