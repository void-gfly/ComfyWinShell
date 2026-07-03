namespace WpfDesktop.Models;

/// <summary>
/// 自定义节点安装结果。
/// </summary>
public sealed class CustomNodeInstallResult
{
    public bool Success { get; set; }

    public string NodeName { get; set; } = string.Empty;

    public string NodePath { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public static CustomNodeInstallResult Ok(string nodeName, string nodePath)
    {
        return new CustomNodeInstallResult
        {
            Success = true,
            NodeName = nodeName,
            NodePath = nodePath
        };
    }

    public static CustomNodeInstallResult Fail(string message, string nodeName = "", string nodePath = "")
    {
        return new CustomNodeInstallResult
        {
            Success = false,
            ErrorMessage = message,
            NodeName = nodeName,
            NodePath = nodePath
        };
    }
}
