using System.Threading;
using System.Threading.Tasks;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// ComfyUI 运行环境检测服务接口
/// </summary>
public interface IEnvironmentCheckService
{
    /// <summary>
    /// 检测事件：每完成一项检测触发一次
    /// </summary>
    event EventHandler<EnvironmentCheckEventArgs>? CheckProgressUpdated;

    /// <summary>
    /// 执行完整的环境检测
    /// </summary>
    /// <param name="pythonPath">Python 解释器路径（可选，如为空则使用系统 PATH）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<EnvironmentCheckResult> CheckAllAsync(string? pythonPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向日志输出启动信息横幅（程序版本、Python、PyTorch、GPU）
    /// </summary>
    /// <param name="pythonPath">Python 解释器路径（可选，如为空则使用系统 PATH）</param>
    /// <param name="comfyRootPath">ComfyUI 根目录路径（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task LogStartupBannerAsync(string? pythonPath, string? comfyRootPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 环境检测结果
/// </summary>
public sealed class EnvironmentCheckResult
{
    public CheckItemResult CudaSupport { get; set; } = new();
    public CheckItemResult AmdSupport { get; set; } = new();
    public CheckItemResult PyTorchAvailable { get; set; } = new();
    public CheckItemResult FFmpegAvailable { get; set; } = new();
    public CheckItemResult GitAvailable { get; set; } = new();
    public CheckItemResult PythonVersion { get; set; } = new();

    /// <summary>
    /// 是否所有必需项都通过
    /// </summary>
    public bool AllRequiredPassed =>
        PyTorchAvailable.Status == CheckStatus.Success &&
        GitAvailable.Status == CheckStatus.Success &&
        PythonVersion.Status == CheckStatus.Success;
}

/// <summary>
/// 单项检测结果
/// </summary>
public sealed class CheckItemResult
{
    public string Name { get; set; } = string.Empty;
    public CheckStatus Status { get; set; } = CheckStatus.Pending;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

/// <summary>
/// 检测状态
/// </summary>
public enum CheckStatus
{
    Pending,    // 未检测
    Running,    // 检测中
    Success,    // 成功
    Warning,    // 警告（非必需项未通过）
    Error       // 错误（必需项未通过）
}

/// <summary>
/// 检测进度事件参数
/// </summary>
public sealed class EnvironmentCheckEventArgs : EventArgs
{
    public string ItemName { get; set; } = string.Empty;
    public CheckStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}
