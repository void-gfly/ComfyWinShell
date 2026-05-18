using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 共享弹性执行策略。
/// </summary>
public sealed class ResiliencePolicyService : IResiliencePolicyService
{
    private readonly ILogService _logService;
    private readonly ResiliencePipeline _pipeline;

    public ResiliencePolicyService(ILogService logService)
        : this(logService, new ResiliencePolicyOptions())
    {
    }

    public ResiliencePolicyService(ILogService logService, ResiliencePolicyOptions options)
    {
        _logService = logService;
        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.OperationTimeout,
                OnTimeout = args =>
                {
                    LogResilienceEvent(args.Context.OperationKey, $"整体执行超时（{args.Timeout.TotalSeconds:F0} 秒）", GUILogLevel.Warning);
                    return default;
                }
            })
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = args => ValueTask.FromResult(ShouldHandle(args.Outcome.Exception)),
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                MaxDelay = options.MaxRetryDelay,
                OnRetry = args =>
                {
                    var reason = DescribeException(args.Outcome.Exception);
                    LogResilienceEvent(
                        args.Context.OperationKey,
                        $"第 {args.AttemptNumber + 1} 次尝试失败，准备指数回退重试：{reason}",
                        GUILogLevel.Warning);
                    return default;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.AttemptTimeout,
                OnTimeout = args =>
                {
                    LogResilienceEvent(args.Context.OperationKey, $"单次执行超时（{args.Timeout.TotalSeconds:F0} 秒）", GUILogLevel.Warning);
                    return default;
                }
            })
            .Build();
    }

    public async Task ExecuteAsync(
        string operationKey,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var context = CreateContext(operationKey, cancellationToken);
        try
        {
            await _pipeline.ExecuteAsync(
                static (ctx, state) => new ValueTask(state(ctx.CancellationToken)),
                context,
                action).ConfigureAwait(false);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    public async Task<T> ExecuteAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var context = CreateContext(operationKey, cancellationToken);
        try
        {
            return await _pipeline.ExecuteAsync(
                static (ctx, state) => new ValueTask<T>(state(ctx.CancellationToken)),
                context,
                action).ConfigureAwait(false);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private static ResilienceContext CreateContext(string operationKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            return ResilienceContextPool.Shared.Get(cancellationToken);
        }

        return ResilienceContextPool.Shared.Get(
            operationKey: operationKey,
            cancellationToken: cancellationToken);
    }

    private static bool ShouldHandle(Exception? exception)
    {
        if (exception == null || exception is OperationCanceledException)
        {
            return false;
        }

        if (exception is TimeoutRejectedException or TimeoutException or IOException)
        {
            return true;
        }

        return GlobalExceptionPolicy.IsRecoverableNetworkException(exception);
    }

    private void LogResilienceEvent(string? operationKey, string message, GUILogLevel level)
    {
        var name = string.IsNullOrWhiteSpace(operationKey) ? "未知操作" : operationKey;
        _logService.Log($"[{name}] {message}", level);
    }

    private static string DescribeException(Exception? exception)
    {
        return exception == null
            ? "未知异常"
            : $"{exception.GetType().Name}: {exception.Message}";
    }
}

/// <summary>
/// 共享弹性策略参数。
/// </summary>
public sealed record ResiliencePolicyOptions
{
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(8);

    public bool UseJitter { get; init; } = true;

    public int MaxRetryAttempts { get; init; } = 3;
}
