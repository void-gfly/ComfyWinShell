using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace WpfDesktop.Views;

/// <summary>
/// Git Clone 进度对话框
/// </summary>
public partial class GitCloneDialog : Window, INotifyPropertyChanged
{
    private readonly string _gitUrl;
    private readonly string _targetDir;
    private readonly string _workingDir;
    private Process? _process;
    private CancellationTokenSource? _cts;

    private string _logOutput = string.Empty;
    private string _statusText = "准备中...";
    private bool _isCompleted;
    private bool _isSuccess;
    private Brush _statusColor = Brushes.Orange;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string GitUrl => _gitUrl;
    public string TargetPath => Path.Combine(_workingDir, _targetDir);

    public string LogOutput
    {
        get => _logOutput;
        private set { _logOutput = value; OnPropertyChanged(nameof(LogOutput)); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        private set { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); OnPropertyChanged(nameof(CanCancel)); }
    }

    public bool CanCancel => !_isCompleted;

    public bool IsSuccess => _isSuccess;

    public Brush StatusColor
    {
        get => _statusColor;
        private set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
    }

    public GitCloneDialog(string gitUrl, string targetDir, string workingDir)
    {
        _gitUrl = gitUrl;
        _targetDir = targetDir;
        _workingDir = workingDir;

        InitializeComponent();
        DataContext = this;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ExecuteGitCloneAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isCompleted)
        {
            var result = MessageBox.Show(
                "安装正在进行中，确定要取消吗？",
                "确认取消",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            CancelOperation();
        }
    }

    private async Task ExecuteGitCloneAsync()
    {
        _cts = new CancellationTokenSource();
        StatusText = "正在克隆...";
        StatusColor = Brushes.DodgerBlue;

        AppendLog($"$ git clone \"{_gitUrl}\" \"{_targetDir}\"\n");
        AppendLog($"工作目录: {_workingDir}\n");
        AppendLog(new string('-', 50) + "\n");

        Directory.CreateDirectory(_workingDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone --progress \"{_gitUrl}\" \"{_targetDir}\"",
            WorkingDirectory = _workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            _process = new Process { StartInfo = startInfo };
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            await _process.WaitForExitAsync(_cts.Token);

            var targetPath = Path.Combine(_workingDir, _targetDir);
            if (_process.ExitCode == 0 && Directory.Exists(targetPath))
            {
                _isSuccess = true;
                StatusText = "安装成功";
                StatusColor = Brushes.LimeGreen;
                AppendLog("\n" + new string('-', 50));
                AppendLog("\n✓ ComfyUI-Manager 安装成功！重启 ComfyUI 后生效。\n");
            }
            else
            {
                _isSuccess = false;
                StatusText = $"安装失败 (退出码: {_process.ExitCode})";
                StatusColor = Brushes.Red;
                AppendLog("\n" + new string('-', 50));
                AppendLog($"\n✗ 安装失败，退出码: {_process.ExitCode}\n");
            }
        }
        catch (OperationCanceledException)
        {
            _isSuccess = false;
            StatusText = "已取消";
            StatusColor = Brushes.Orange;
            AppendLog("\n" + new string('-', 50));
            AppendLog("\n⚠ 操作已被用户取消\n");
        }
        catch (Exception ex)
        {
            _isSuccess = false;
            StatusText = "发生错误";
            StatusColor = Brushes.Red;
            AppendLog("\n" + new string('-', 50));
            AppendLog($"\n✗ 错误: {ex.Message}\n");

            if (ex.Message.Contains("git") || ex is Win32Exception)
            {
                AppendLog("\n提示: 请确保已安装 Git 并添加到系统 PATH 环境变量。\n");
            }
        }
        finally
        {
            IsCompleted = true;
            _process?.Dispose();
            _process = null;
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Dispatcher.BeginInvoke(() => AppendLog(e.Data + "\n"));
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        // Git 的进度信息通过 stderr 输出
        if (!string.IsNullOrEmpty(e.Data))
        {
            Dispatcher.BeginInvoke(() => AppendLog(e.Data + "\n"));
        }
    }

    private void AppendLog(string text)
    {
        LogOutput += text;
    }

    private void CancelOperation()
    {
        try
        {
            _cts?.Cancel();
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 忽略取消时的异常
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定要取消安装吗？",
            "确认取消",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            CancelOperation();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _isSuccess;
        Close();
    }

    private void LogTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        LogScrollViewer.ScrollToEnd();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
