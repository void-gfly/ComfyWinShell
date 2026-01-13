using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface IConfigurationService
{
    Task<ComfyConfiguration> LoadConfigurationAsync(string profileId);
    Task SaveConfigurationAsync(string profileId, ComfyConfiguration configuration);
    Task<bool> ValidateConfigurationAsync(ComfyConfiguration configuration);
}
