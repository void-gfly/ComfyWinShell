using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

public interface IProcessService
{
    event EventHandler<ProcessStatus>? StatusChanged;
    event EventHandler<string>? OutputReceived;
    event EventHandler<bool>? HeartbeatStatusChanged;
    event EventHandler<string>? SystemStatsUpdated;

    /// <summary>
    /// 配置用于健康检查与状态采集的 ComfyUI API 端点
    /// </summary>
    /// <param name="listen">监听地址</param>
    /// <param name="port">端口</param>
    void ConfigureApiEndpoint(string listen, int port);

    /// <summary>
    /// 启动 ComfyUI 进程
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录（包含 python_embeded、ComfyUI 子目录的目录）</param>
    /// <param name="configuration">启动配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> StartAsync(string comfyRootPath, ComfyConfiguration configuration, CancellationToken cancellationToken = default);

    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动前清理残留的 ComfyUI Python 进程（精准匹配当前配置的 python 与 main.py）
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理到的进程数量</returns>
    Task<int> CleanupLingeringProcessesAsync(string comfyRootPath, CancellationToken cancellationToken = default);

    Task<bool> RequestGracefulStopAsync(CancellationToken cancellationToken = default);
    Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<ProcessStatus?> GetStatusAsync(CancellationToken cancellationToken = default);
}
