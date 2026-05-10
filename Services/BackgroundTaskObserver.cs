using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 后台任务异常观察器。
/// </summary>
public static class BackgroundTaskObserver
{
    /// <summary>
    /// 观察 fire-and-forget 任务的异常，并将结果写入日志。
    /// </summary>
    /// <param name="task">待观察任务。</param>
    /// <param name="logService">日志服务。</param>
    /// <param name="source">任务来源说明。</param>
    public static void Observe(Task? task, ILogService? logService, string source)
    {
        if (task == null)
        {
            return;
        }

        if (task.IsCompletedSuccessfully)
        {
            return;
        }

        _ = task.ContinueWith(
            completedTask => LogFaultedTask(completedTask, logService, source),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void LogFaultedTask(Task task, ILogService? logService, string source)
    {
        try
        {
            var exception = task.Exception?.Flatten().InnerExceptions.Count == 1
                ? task.Exception!.Flatten().InnerExceptions[0]
                : task.Exception;

            if (GlobalExceptionPolicy.IsRecoverableNetworkException(exception))
            {
                var summary = exception == null
                    ? source
                    : $"{source}: {exception.GetType().Name} - {exception.Message}";
                logService?.Log(summary, GUILogLevel.Warning);
                return;
            }

            logService?.LogError(source, exception);
        }
        catch
        {
            // 后台异常观察失败不应再抛出新异常
        }
    }
}
