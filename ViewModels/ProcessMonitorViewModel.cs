using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.ViewModels;

public partial class ProcessMonitorViewModel : ViewModelBase
{
    private readonly IProcessService _processService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly IEnvironmentCheckService _environmentCheckService;
    private readonly IPythonPathService _pythonPathService;
    private int _maxLogLines = 5000;

    // tqdm 进度条模式匹配：匹配类似 "  5%|█         | 1/20 [00:10<03:24, 10.77s/it]" 的输出
    private static readonly Regex TqdmProgressPattern = new(
        @"^\s*\d+%\|.*\|\s*\d+/\d+\s*\[",
        RegexOptions.Compiled);

    // 记录最后一条进度行在 _allLogs 中的索引，-1 表示没有
    private int _lastProgressLineIndex = -1;

    public ProcessMonitorViewModel(
        IProcessService processService, 
        ISettingsService settingsService, 
        ILogService logService,
        IEnvironmentCheckService environmentCheckService,
        IPythonPathService pythonPathService)
    {
        _processService = processService;
        _settingsService = settingsService;
        _logService = logService;
        _environmentCheckService = environmentCheckService;
        _pythonPathService = pythonPathService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ClearLogsCommand = new RelayCommand(ClearLogs);
        CopySelectedLogsCommand = new RelayCommand<IList>(CopySelectedLogs);
        CopyAllLogsCommand = new RelayCommand(CopyAllLogs, () => Logs.Count > 0);
        CheckEnvironmentCommand = new AsyncRelayCommand(CheckEnvironmentAsync, () => !IsCheckingEnvironment);

        _processService.StatusChanged += OnStatusChanged;
        _processService.OutputReceived += OnOutputReceived;
        // 只订阅 LogEntryReceived，避免与 LogReceived 重复（两者同时触发）
        _logService.LogEntryReceived += OnLogEntryReceived;
        WeakReferenceMessenger.Default.Register<AppSettingsChangedMessage>(this, (_, message) =>
        {
            _maxLogLines = message.Value.MaxLogLines > 0 ? message.Value.MaxLogLines : 5000;
            LogLineHeight = ResolveLogLineHeight(message.Value.LogLineHeight);
        });

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var settings = await _settingsService.LoadAsync();
        _maxLogLines = settings.MaxLogLines > 0 ? settings.MaxLogLines : 5000;
        LogLineHeight = ResolveLogLineHeight(settings.LogLineHeight);
        await RefreshAsync();
    }

    private readonly List<LogEntry> _allLogs = new();

    public ObservableCollection<LogEntry> Logs { get; } = new();

    [ObservableProperty]
    private ProcessStatus? _status;

    [ObservableProperty]
    private string _statusText = "未运行";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private double _logLineHeight = 15;

    /// <summary>
    /// 用于在页面切换时保存滚动条位置
    /// </summary>
    public double SavedScrollOffset { get; set; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ClearLogsCommand { get; }

    public IRelayCommand<IList> CopySelectedLogsCommand { get; }

    public IRelayCommand CopyAllLogsCommand { get; }

    public IAsyncRelayCommand CheckEnvironmentCommand { get; }

    [ObservableProperty]
    private bool _isCheckingEnvironment;

    private async Task CheckEnvironmentAsync()
    {
        IsCheckingEnvironment = true;
        CheckEnvironmentCommand.NotifyCanExecuteChanged();

        try
        {
            // 获取当前配置的 Python 路径
            var pythonPath = _pythonPathService.PythonPath;

            _logService.Log("正在执行环境检测...");

            await _environmentCheckService.CheckAllAsync(pythonPath);

            _logService.Log("环境检测完成。");
        }
        catch (Exception ex)
        {
            _logService.LogError($"环境检测失败: {ex.Message}");
        }
        finally
        {
            IsCheckingEnvironment = false;
            CheckEnvironmentCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RefreshAsync()
    {
        Status = await _processService.GetStatusAsync();
        if (Status == null)
        {
            StatusText = "未运行";
            IsRunning = false;
            return;
        }

        StatusText = Status.State switch
        {
            ProcessState.Starting => "启动中",
            ProcessState.Running => "运行中",
            ProcessState.Stopping => "停止中",
            ProcessState.Stopped => "已停止",
            ProcessState.Error => "异常",
            _ => "空闲"
        };
        IsRunning = Status.IsRunning;
    }

    private void ClearLogs()
    {
        _allLogs.Clear();
        Logs.Clear();
        CopyAllLogsCommand.NotifyCanExecuteChanged();
    }

    private void CopySelectedLogs(IList? selectedItems)
    {
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        var lines = selectedItems.Cast<LogEntry>().Select(e => e.FormattedMessage).ToList();
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    private void CopyAllLogs()
    {
        if (Logs.Count > 0)
        {
            var lines = Logs.Select(e => e.FormattedMessage);
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
    }

    private void OnStatusChanged(object? sender, ProcessStatus status)
    {
        RunOnUiThread(() =>
        {
            Status = status;
            StatusText = status.State switch
            {
                ProcessState.Starting => "启动中",
                ProcessState.Running => "运行中",
                ProcessState.Stopping => "停止中",
                ProcessState.Stopped => "已停止",
                ProcessState.Error => "异常",
                _ => "空闲"
            };
            IsRunning = status.IsRunning;
        });
    }

    private void OnOutputReceived(object? sender, string line)
    {
        // 进程输出默认为普通日志（白色）
        AddLogEntry(new LogEntry
        {
            Message = line,
            Level = GUILogLevel.Info,
            Timestamp = DateTime.Now
        });
    }

    private void OnLogEntryReceived(object? sender, LogEntry entry)
    {
        AddLogEntry(entry);
    }

    private void AddLogEntry(LogEntry entry)
    {
        RunOnUiThread(() =>
        {
            var isProgressLine = TqdmProgressPattern.IsMatch(entry.Message);

            // 如果是进度行且之前已有进度行，则替换而非添加
            if (isProgressLine && _lastProgressLineIndex >= 0 && _lastProgressLineIndex < _allLogs.Count)
            {
                var oldProgressEntry = _allLogs[_lastProgressLineIndex];
                _allLogs[_lastProgressLineIndex] = entry;

                // 同步更新 Logs 中对应的行
                var logsIndex = FindIndexInLogs(oldProgressEntry, _lastProgressLineIndex);
                if (logsIndex >= 0)
                {
                    Logs[logsIndex] = entry;
                }
            }
            else
            {
                // 普通日志或首条进度行：添加新行
                _allLogs.Add(entry);

                if (isProgressLine)
                {
                    _lastProgressLineIndex = _allLogs.Count - 1;
                }
                else
                {
                    // 非进度行出现后，重置进度行追踪（进度已结束）
                    _lastProgressLineIndex = -1;
                }

                // 限制总行数
                while (_allLogs.Count > _maxLogLines)
                {
                    var removed = _allLogs[0];
                    _allLogs.RemoveAt(0);

                    // 如果删除的行在进度行之前，需要调整索引
                    if (_lastProgressLineIndex > 0)
                    {
                        _lastProgressLineIndex--;
                    }

                    if (Logs.Count > 0 && Logs[0] == removed)
                    {
                        Logs.RemoveAt(0);
                    }
                }

                if (string.IsNullOrWhiteSpace(FilterText)
                    || entry.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                {
                    Logs.Add(entry);
                    CopyAllLogsCommand.NotifyCanExecuteChanged();
                }
            }
        });
    }

    /// <summary>
    /// 在 Logs 集合中查找对应 _allLogs[allLogsIndex] 的行索引
    /// </summary>
    private int FindIndexInLogs(LogEntry targetEntry, int allLogsIndex)
    {
        // 如果没有过滤，Logs 和 _allLogs 顺序一致（但可能因行数限制有偏移）
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            // 计算 Logs 中的对应索引
            var offset = _allLogs.Count - Logs.Count;
            var logsIndex = allLogsIndex - offset;
            if (logsIndex >= 0 && logsIndex < Logs.Count && Logs[logsIndex] == targetEntry)
            {
                return logsIndex;
            }
        }

        // 有过滤或索引不匹配时，从后向前查找（进度行通常在末尾附近）
        for (var i = Logs.Count - 1; i >= 0; i--)
        {
            if (Logs[i] == targetEntry)
            {
                return i;
            }
        }

        return -1;
    }

    partial void OnFilterTextChanged(string value)
    {
        RunOnUiThread(() =>
        {
            Logs.Clear();

            if (string.IsNullOrWhiteSpace(value))
            {
                foreach (var entry in _allLogs)
                {
                    Logs.Add(entry);
                }
            }
            else
            {
                foreach (var entry in _allLogs)
                {
                    if (entry.Message.Contains(value, StringComparison.OrdinalIgnoreCase))
                    {
                        Logs.Add(entry);
                    }
                }
            }

            CopyAllLogsCommand.NotifyCanExecuteChanged();
        });
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action);
    }

    private static double ResolveLogLineHeight(int value)
    {
        return value is 12 or 15 or 18 ? value : 15;
    }
}
