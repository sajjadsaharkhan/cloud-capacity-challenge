using Arvan.Capacity.Application.Common.Exceptions;

namespace Arvan.Capacity.Application.Operations.Idempotency;

/// <summary>
/// Owns the idempotency keys: claiming one inside the caller's transaction, replaying the stored
/// response of a completed one, and storing the response of a new one. Never begins or commits a
/// transaction, never calls a peer Operation.
/// </summary>
public interface IIdempotencyOperations
{
    /// <summary>
    /// Claims a key for the current transaction. A retry of a completed call comes back as a replay
    /// carrying the stored response; the caller returns it and changes nothing.
    /// </summary>
    /// <typeparam name="T">The response type stored with the key.</typeparam>
    /// <param name="customerId">The key's namespace.</param>
    /// <param name="key">The client's key.</param>
    /// <param name="operation">The operation the key is bound to, such as <c>submit</c> or <c>confirm:{id}</c>.</param>
    /// <param name="requestBody">The request's meaningful content; its fingerprint is what a retry must match.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="IdempotencyKeyReusedException">Thrown when the key already identifies a different operation or body.</exception>
    Task<IdempotencyClaim<T>> ClaimAsync<T>(
        string customerId,
        string key,
        string operation,
        object requestBody,
        CancellationToken ct) where T : class;

    /// <summary>
    /// Stores the response a claimed key will replay. Called last, in the same transaction.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="customerId">The key's namespace.</param>
    /// <param name="key">The client's key.</param>
    /// <param name="reservationId">The reservation the call concerned.</param>
    /// <param name="response">The response to store.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task CompleteAsync<T>(string customerId, string key, Guid? reservationId, T response, CancellationToken ct)
        where T : class;

    /// <summary>
    /// Deletes keys past their retention, up to <paramref name="limit"/> of them.
    /// </summary>
    /// <param name="limit">The largest number of keys to delete.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <returns>The number deleted.</returns>
    Task<int> PurgeExpiredAsync(int limit, CancellationToken ct);
}

/// <summary>
/// What claiming an idempotency key found.
/// </summary>
/// <typeparam name="T">The response type stored with the key.</typeparam>
/// <param name="IsReplay">Whether the key was already completed by an earlier call.</param>
/// <param name="Response">The stored response when <paramref name="IsReplay"/>; otherwise <c>null</c>.</param>
public sealed record IdempotencyClaim<T>(bool IsReplay, T? Response) where T : class;
