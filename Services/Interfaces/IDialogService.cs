namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 通用对话框服务接口。
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 打开文件夹选择对话框。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="initialDirectory">初始目录。</param>
    /// <returns>用户选择的文件夹路径；取消时返回 null。</returns>
    string? SelectFolder(string title, string? initialDirectory = null);

    /// <summary>
    /// 打开文件选择对话框。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="filter">文件筛选器。</param>
    /// <param name="initialDirectory">初始目录。</param>
    /// <returns>用户选择的文件路径；取消时返回 null。</returns>
    string? SelectFile(string title, string? filter = null, string? initialDirectory = null);

    /// <summary>
    /// 打开文件保存对话框。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="defaultFileName">默认文件名。</param>
    /// <param name="filter">文件筛选器。</param>
    /// <param name="initialDirectory">初始目录。</param>
    /// <returns>用户确认后的保存路径；取消时返回 null。</returns>
    string? SaveFile(string title, string? defaultFileName = null, string? filter = null, string? initialDirectory = null);

    /// <summary>
    /// 显示确认对话框
    /// </summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">对话框标题。</param>
    /// <returns>用户确认时返回 true，否则返回 false。</returns>
    bool Confirm(string message, string title = "确认");

    /// <summary>
    /// 显示信息对话框
    /// </summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">对话框标题。</param>
    void ShowInfo(string message, string title = "信息");

    /// <summary>
    /// 显示错误对话框
    /// </summary>
    /// <param name="message">错误内容。</param>
    /// <param name="title">对话框标题。</param>
    void ShowError(string message, string title = "错误");
}
