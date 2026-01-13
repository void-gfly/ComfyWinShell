using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface IProcessService
{
    event EventHandler<ProcessStatus>? StatusChanged;
    event EventHandler<string>? OutputReceived;

    /// <summary>
    /// 启动 ComfyUI 进程
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录（包含 python_embeded、ComfyUI 子目录的目录）</param>
    /// <param name="configuration">启动配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> StartAsync(string comfyRootPath, ComfyConfiguration configuration, CancellationToken cancellationToken = default);

    Task<bool> StopAsync(CancellationToken cancellationToken = default);
    Task<bool> RequestGracefulStopAsync(CancellationToken cancellationToken = default);
    Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<ProcessStatus?> GetStatusAsync(CancellationToken cancellationToken = default);
}
