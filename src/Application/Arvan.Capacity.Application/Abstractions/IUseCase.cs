namespace Arvan.Capacity.Application.Abstractions;

/// <summary>
/// One business operation the outside world can invoke. Every entry point into the
/// Application layer is a UseCase — reads included; there is no separate query tier.
/// </summary>
/// <remarks>
/// This interface exists to force every UseCase into the same signature, and to give a
/// cross-cutting pipeline a seam to attach to if one is ever wanted. It is not a
/// substitution seam: callers inject the concrete UseCase class.
/// </remarks>
/// <typeparam name="TInput">The UseCase's input contract.</typeparam>
/// <typeparam name="TOutput">The UseCase's output contract.</typeparam>
public interface IUseCase<in TInput, TOutput>
{
    /// <summary>
    /// Runs the use case.
    /// </summary>
    /// <param name="input">Everything the use case needs to do its work.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct);
}

/// <summary>
/// A UseCase that has nothing to report back to its caller. Prefer
/// <see cref="IUseCase{TInput,TOutput}"/> unless the operation genuinely produces no output.
/// </summary>
/// <typeparam name="TInput">The UseCase's input contract.</typeparam>
public interface IUseCase<in TInput>
{
    /// <summary>
    /// Runs the use case.
    /// </summary>
    /// <param name="input">Everything the use case needs to do its work.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task ExecuteAsync(TInput input, CancellationToken ct);
}
