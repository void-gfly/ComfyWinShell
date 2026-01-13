namespace WpfDesktop.Services.Interfaces;

public interface IDialogService
{
    string? SelectFolder(string title, string? initialDirectory = null);
    string? SelectFile(string title, string? filter = null, string? initialDirectory = null);
    string? SaveFile(string title, string? defaultFileName = null, string? filter = null, string? initialDirectory = null);

    /// <summary>
    /// 显示确认对话框
    /// </summary>
    bool Confirm(string message, string title = "确认");

    /// <summary>
    /// 显示信息对话框
    /// </summary>
    void ShowInfo(string message, string title = "信息");

    /// <summary>
    /// 显示错误对话框
    /// </summary>
    void ShowError(string message, string title = "错误");
}
