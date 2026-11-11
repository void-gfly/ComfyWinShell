using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 在当前 ComfyUI 的 Python 环境中升级 ComfyUI-Manager。
/// </summary>
public interface IComfyManagerUpgradeService
{
    /// <summary>
    /// 执行 python -m pip install -U comfyui-manager。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>升级结果。pip 退出码为 0 时成功。</returns>
    Task<ComfyManagerUpgradeResult> UpgradeAsync(CancellationToken cancellationToken = default);
}
