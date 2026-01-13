using System.Threading.Tasks;

namespace WpfDesktop.Services.Interfaces;

public interface INavigationAware
{
    Task OnNavigatedToAsync();
}
