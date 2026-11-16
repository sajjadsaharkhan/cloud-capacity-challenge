namespace Arvan.Capacity.Application.Abstractions;

/// <summary>
/// One multi-aggregate sequence, run inside a transaction its caller already opened. A Flow
/// (Tier 2) sequences two or more aggregates through their Operations; a sequence touching a
/// single aggregate is that aggregate's Operation, not a Flow.
/// </summary>
/// <remarks>
/// A Flow never begins, commits, or flushes a transaction. <see cref="ITransactionManager"/> is
/// not injectable here, and — the half this signature exists to enforce — control over it is not
/// accepted as a parameter either: <c>ExecuteAsync</c> takes one input and a token, so there is
/// nowhere to hand a flush delegate down to. Transaction flow is owned end to end by the UseCase.
/// A Flow reaches persistence only through <c>I{Aggregate}Operations</c>, never a Repository, and
/// never calls a peer Flow or a UseCase.
/// <para>
/// Like <see cref="IUseCase{TInput,TOutput}"/> this is a shape constraint, not a substitution
/// seam: callers inject the concrete Flow class.
/// </para>
/// </remarks>
/// <typeparam name="TInput">The Flow's input contract.</typeparam>
/// <typeparam name="TOutput">What the Flow hands back — an entity or a primitive, never a UseCase's Output.</typeparam>
public interface IFlow<in TInput, TOutput>
{
    /// <summary>
    /// Runs the flow inside the caller's open transaction.
    /// </summary>
    /// <param name="input">Everything the flow needs to do its work.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct);
}
