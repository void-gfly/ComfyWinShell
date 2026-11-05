using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface IGpuPowerService
{
    Task<IReadOnlyList<GpuPowerDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task SetPowerLimitAsync(string uuid, decimal watts, CancellationToken cancellationToken = default);
}
