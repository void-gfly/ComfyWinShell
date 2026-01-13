using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}
