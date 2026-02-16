using System.Windows;

namespace WpfDesktop.Views;

public partial class GlobalExceptionDialog : Window
{
    public string ExceptionDetails { get; }

    public GlobalExceptionDialog(string exceptionDetails)
    {
        ExceptionDetails = exceptionDetails;
        InitializeComponent();
        DataContext = this;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ExceptionDetails ?? string.Empty);
        }
        catch
        {
            // 剪贴板不可用时静默，避免异常处理对话框再次抛错
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
