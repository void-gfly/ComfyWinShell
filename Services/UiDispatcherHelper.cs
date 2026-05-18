using System.Diagnostics;
using System.Windows.Threading;

namespace WpfDesktop.Services;

internal static class UiDispatcherHelper
{
    public static void TryInvoke(Dispatcher? dispatcher, Action action, string source)
    {
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = dispatcher.BeginInvoke(action);
        }
        catch (ObjectDisposedException ex)
        {
            Debug.WriteLine($"[{source}] 调度到 UI 线程失败: {ex}");
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"[{source}] 调度到 UI 线程失败: {ex}");
        }
    }
}
