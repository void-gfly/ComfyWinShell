namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 共享的外部依赖弹性执行入口。
/// </summary>
public interface IResiliencePolicyService
{
    /// <summary>
    /// 在共享弹性策略下执行无返回值操作。
    /// </summary>
    Task ExecuteAsync(
        string operationKey,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在共享弹性策略下执行有返回值操作。
    /// </summary>
    Task<T> ExecuteAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}
