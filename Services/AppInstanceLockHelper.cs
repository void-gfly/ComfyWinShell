using System.Diagnostics;
using System.Text;
using System.Threading;

namespace WpfDesktop.Services;

/// <summary>
/// 应用实例互斥辅助类。
/// </summary>
public static class AppInstanceLockHelper
{
    private const string DefaultAppName = "ComfyShell";
    private const string MutexPrefix = @"Local\ComfyShell.AppInstance.";

    /// <summary>
    /// 根据应用名称生成命名互斥体名称。
    /// </summary>
    /// <param name="appName">应用名称。</param>
    /// <returns>可用于命名互斥体的名称。</returns>
    public static string BuildMutexName(string? appName)
    {
        return MutexPrefix + NormalizeAppName(appName);
    }

    /// <summary>
    /// 尝试获取当前应用实例互斥。
    /// </summary>
    /// <param name="appName">应用名称。</param>
    /// <returns>获取成功时返回锁对象，否则返回 null。</returns>
    public static AppInstanceLock? TryAcquire(string? appName)
    {
        var mutex = new Mutex(false, BuildMutexName(appName));
        try
        {
            if (mutex.WaitOne(0, false))
            {
                return new AppInstanceLock(mutex);
            }
        }
        catch (AbandonedMutexException)
        {
            return new AppInstanceLock(mutex);
        }

        mutex.Dispose();
        return null;
    }

    private static string NormalizeAppName(string? appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            return DefaultAppName;
        }

        var builder = new StringBuilder(appName.Length);
        var lastWasSeparator = false;

        foreach (var ch in appName.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')
            {
                builder.Append(ch);
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator)
            {
                continue;
            }

            builder.Append('_');
            lastWasSeparator = true;
        }

        var normalized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? DefaultAppName : normalized;
    }
}

/// <summary>
/// 当前应用实例持有的互斥句柄。
/// </summary>
public sealed class AppInstanceLock : IDisposable
{
    private Mutex? _mutex;
    private bool _disposed;

    internal AppInstanceLock(Mutex mutex)
    {
        _mutex = mutex;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex == null)
        {
            return;
        }

        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException ex)
        {
            Debug.WriteLine($"释放应用实例互斥失败: {ex}");
        }
        finally
        {
            mutex.Dispose();
        }
    }
}
