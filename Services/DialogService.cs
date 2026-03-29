using System.Windows;
using Win32 = Microsoft.Win32;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 基于系统对话框的交互服务。
/// </summary>
public class DialogService : IDialogService
{
    /// <summary>
    /// 打开文件夹选择对话框。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="initialDirectory">初始目录。</param>
    /// <returns>用户选择的文件夹路径；取消时返回 null。</returns>
    public string? SelectFolder(string title, string? initialDirectory = null)
    {
        var dialog = new Win32.OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory ?? string.Empty,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <summary>
    /// 打开文件选择对话框。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="filter">文件筛选器。</param>
    /// <param name="initialDirectory">初始目录。</param>
    /// <returns>用户选择的文件路径；取消时返回 null。</returns>
    public string? SelectFile(string title, string? filter = null, string? initialDirectory = null)
    {
        var dialog = new Win32.OpenFileDialog
        {
            Title = title,
            Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter,
            InitialDirectory = initialDirectory
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// 打开文件保存对话框。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="defaultFileName">默认文件名。</param>
    /// <param name="filter">文件筛选器。</param>
    /// <param name="initialDirectory">初始目录。</param>
    /// <returns>用户确认后的保存路径；取消时返回 null。</returns>
    public string? SaveFile(string title, string? defaultFileName = null, string? filter = null, string? initialDirectory = null)
    {
        var dialog = new Win32.SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName ?? string.Empty,
            Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter,
            InitialDirectory = initialDirectory
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// 显示确认对话框并返回用户选择结果。
    /// </summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">对话框标题。</param>
    /// <returns>用户确认时返回 true，否则返回 false。</returns>
    public bool Confirm(string message, string title = "确认")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// 显示普通信息提示框。
    /// </summary>
    /// <param name="message">提示内容。</param>
    /// <param name="title">对话框标题。</param>
    public void ShowInfo(string message, string title = "信息")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// 显示错误提示框。
    /// </summary>
    /// <param name="message">错误内容。</param>
    /// <param name="title">对话框标题。</param>
    public void ShowError(string message, string title = "错误")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
