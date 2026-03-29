using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// ComfyUI 进程管理服务接口。
/// </summary>
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
    /// <returns>启动成功返回 true，否则返回 false。</returns>
    Task<bool> StartAsync(string comfyRootPath, ComfyConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止当前 ComfyUI 进程。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功停止或进程已停止时返回 true，否则返回 false。</returns>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动前清理残留的 ComfyUI Python 进程（精准匹配当前配置的 python 与 main.py）
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清理到的进程数量</returns>
    Task<int> CleanupLingeringProcessesAsync(string comfyRootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求 ComfyUI 执行优雅停止。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已成功发送停止请求时返回 true，否则返回 false。</returns>
    Task<bool> RequestGracefulStopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 在指定超时时间内等待进程退出。
    /// </summary>
    /// <param name="timeout">等待超时时长。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程在超时前退出时返回 true，否则返回 false。</returns>
    Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前进程运行状态快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存在运行中进程时返回状态对象，否则返回 null。</returns>
    Task<ProcessStatus?> GetStatusAsync(CancellationToken cancellationToken = default);
}
