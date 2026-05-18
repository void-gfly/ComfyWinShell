using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Polly.Timeout;

namespace WpfDesktop.Services;

/// <summary>
/// 全局异常处理策略。
/// </summary>
public static class GlobalExceptionPolicy
{
    /// <summary>
    /// 判断异常是否属于可恢复的网络波动或超时类异常。
    /// </summary>
    /// <param name="exception">待检查的异常。</param>
    /// <returns>属于可恢复异常时返回 true，否则返回 false。</returns>
    public static bool IsRecoverableNetworkException(Exception? exception)
    {
        if (exception == null)
        {
            return false;
        }

        return IsRecoverableNetworkExceptionCore(exception);
    }

    private static bool IsRecoverableNetworkExceptionCore(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var inner in aggregateException.InnerExceptions)
            {
                if (IsRecoverableNetworkExceptionCore(inner))
                {
                    return true;
                }
            }

            return aggregateException.InnerException != null &&
                   IsRecoverableNetworkExceptionCore(aggregateException.InnerException);
        }

        if (exception is HttpRequestException or SocketException or WebSocketException or TimeoutRejectedException or TimeoutException or TaskCanceledException or OperationCanceledException or WebException)
        {
            return true;
        }

        return exception.InnerException != null && IsRecoverableNetworkExceptionCore(exception.InnerException);
    }
}
