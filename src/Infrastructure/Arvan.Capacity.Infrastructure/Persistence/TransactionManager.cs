using Arvan.Capacity.Application.Abstractions;

namespace Arvan.Capacity.Infrastructure.Persistence;

/// <inheritdoc cref="ITransactionManager"/>
internal sealed class TransactionManager(PostgresSession session) : ITransactionManager
{
    /// <inheritdoc cref="ITransactionManager.RunAsync{T}(Func{CancellationToken,Task{T}},CancellationToken)"/>
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
        session.RunAsync(work, ct);

    /// <inheritdoc cref="ITransactionManager.RunAsync(Func{CancellationToken,Task},CancellationToken)"/>
    public Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct) =>
        session.RunAsync(async token =>
        {
            await work(token);
            return true;
        }, ct);

    /// <inheritdoc cref="ITransactionManager.AttemptAsync"/>
    public Task<bool> AttemptAsync(Func<CancellationToken, Task<bool>> attempt, CancellationToken ct) =>
        session.AttemptAsync(attempt, ct);
}
