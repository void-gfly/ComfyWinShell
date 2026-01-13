using System.Windows;
using Win32 = Microsoft.Win32;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

public class DialogService : IDialogService
{
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

    public bool Confirm(string message, string title = "确认")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public void ShowInfo(string message, string title = "信息")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message, string title = "错误")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
