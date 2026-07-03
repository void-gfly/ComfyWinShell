using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 外部命令执行器接口。
/// </summary>
public interface IExternalProcessRunner
{
    Task<ExternalProcessResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default);
}
