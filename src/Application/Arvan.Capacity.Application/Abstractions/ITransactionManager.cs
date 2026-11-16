namespace Arvan.Capacity.Application.Abstractions;

/// <summary>
/// Owns the transaction lifecycle against the write database: begin, retry, commit and rollback,
/// plus savepoints inside the open transaction. Injectable only into a UseCase — no Operation,
/// Flow, or Repository may depend on this port.
/// </summary>
/// <remarks>
/// Nor may any of them be handed the capability second-hand: passing <see cref="AttemptAsync"/>
/// down as a delegate parameter honours the injection rule in letter and breaks it in substance.
/// Transaction flow is decided in one place, the UseCase, and everything below it runs unaware
/// that a transaction is what it is running inside.
/// <para>
/// There is no <c>FlushAsync</c>: persistence here is Dapper, with no change tracker, so every
/// repository write is already a statement inside the open transaction.
/// </para>
/// </remarks>
public interface ITransactionManager
{
    /// <summary>
    /// Runs <paramref name="work"/> inside a single READ COMMITTED transaction and commits on
    /// success. The delegate must be re-runnable: a deadlock or serialization failure re-invokes it
    /// from the start, so it must not consume a forward-only stream, mutate state outside itself, or
    /// perform non-database I/O.
    /// </summary>
    /// <param name="work">The transactional body. Receives the same cancellation token passed in.</param>
    /// <param name="ct">Cancels the operation, including any pending retry.</param>
    /// <returns>The value returned by <paramref name="work"/>.</returns>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);

    /// <summary>
    /// Runs <paramref name="work"/> inside a single READ COMMITTED transaction and commits on
    /// success. The delegate must be re-runnable, as for <see cref="RunAsync{T}"/>.
    /// </summary>
    /// <param name="work">The transactional body. Receives the same cancellation token passed in.</param>
    /// <param name="ct">Cancels the operation, including any pending retry.</param>
    Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct);

    /// <summary>
    /// Runs <paramref name="attempt"/> under a savepoint inside the open transaction. When it
    /// returns <c>false</c>, or throws, everything it wrote is rolled back to the savepoint and the
    /// transaction carries on.
    /// </summary>
    /// <param name="attempt">The writes to try; <c>true</c> keeps them, <c>false</c> discards them.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>Whether the attempt was kept.</returns>
    Task<bool> AttemptAsync(Func<CancellationToken, Task<bool>> attempt, CancellationToken ct);
}
