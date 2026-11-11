namespace WpfDesktop.Models;

/// <summary>
/// ComfyUI-Manager 升级结果。
/// </summary>
public sealed class ComfyManagerUpgradeResult
{
    public bool Success { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public static ComfyManagerUpgradeResult Ok()
    {
        return new ComfyManagerUpgradeResult { Success = true };
    }

    public static ComfyManagerUpgradeResult Fail(string message)
    {
        return new ComfyManagerUpgradeResult
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
