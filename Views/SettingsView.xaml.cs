using System.Windows;
using System.Windows.Controls;
using WpfDesktop.ViewModels;

namespace WpfDesktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 加载时设置密码框的值
        if (DataContext is SettingsViewModel vm && !string.IsNullOrEmpty(vm.Settings.Proxy.Password))
        {
            ProxyPasswordBox.Password = vm.Settings.Proxy.Password;
        }
    }

    private void ProxyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        // 密码框变化时更新 ViewModel
        if (DataContext is SettingsViewModel vm)
        {
            vm.Settings.Proxy.Password = ProxyPasswordBox.Password;
        }
    }

    #region GitHub 镜像复制

    private void CopyGitHubMirror1_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard("https://ghproxy.com/https://github.com", sender);
    }

    private void CopyGitHubMirror2_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard("https://mirror.ghproxy.com/https://github.com", sender);
    }

    private void CopyGitHubMirror3_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard("https://gh.api.99988866.xyz/https://github.com", sender);
    }

    #endregion

    #region pip 镜像复制

    private void CopyPipMirror1_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard("https://pypi.tuna.tsinghua.edu.cn/simple", sender);
    }

    private void CopyPipMirror2_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard("https://mirrors.aliyun.com/pypi/simple", sender);
    }

    private void CopyPipMirror3_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard("https://pypi.mirrors.ustc.edu.cn/simple", sender);
    }

    private void CopyPipMirror4_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard("https://mirrors.cloud.tencent.com/pypi/simple", sender);
    }

    #endregion

    private void CopyToClipboard(string text, object sender)
    {
        try
        {
            Clipboard.SetText(text);
            
            // 显示提示信息（可选）
            if (sender is Button button)
            {
                var originalContent = button.Content;
                button.Content = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "✓", FontSize = 14, Margin = new Thickness(0, 0, 4, 0) },
                        new TextBlock { Text = "已复制", FontSize = 11 }
                    }
                };

                // 2秒后恢复原始内容
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    button.Content = originalContent;
                    timer.Stop();
                };
                timer.Start();
            }
        }
        catch
        {
            // 忽略复制失败的错误
        }
    }
}

