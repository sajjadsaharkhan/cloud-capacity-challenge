namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The port for stored idempotency keys. Inserting waits on a concurrent transaction that is using
/// the same key, so a duplicate is never visible half-done.
/// </summary>
public interface IIdempotencyRepository
{
    /// <summary>
    /// Claims a key for the current transaction, or reports what already holds it.
    /// </summary>
    /// <param name="customerId">The key's namespace.</param>
    /// <param name="key">The client's key.</param>
    /// <param name="operation">The operation the key is bound to.</param>
    /// <param name="requestHash">The fingerprint of the request body.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<StoredIdempotencyKey> ClaimAsync(
        string customerId,
        string key,
        string operation,
        byte[] requestHash,
        CancellationToken ct);

    /// <summary>
    /// Stores the response a claimed key will replay.
    /// </summary>
    /// <param name="customerId">The key's namespace.</param>
    /// <param name="key">The client's key.</param>
    /// <param name="reservationId">The reservation the call concerned, if any.</param>
    /// <param name="responseBody">The serialized response.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task CompleteAsync(string customerId, string key, Guid? reservationId, string responseBody, CancellationToken ct);

    /// <summary>
    /// Deletes keys past their retention, up to <paramref name="limit"/> of them.
    /// </summary>
    /// <param name="limit">The largest number of keys to delete.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <returns>The number deleted.</returns>
    Task<int> PurgeExpiredAsync(int limit, CancellationToken ct);
}

/// <summary>
/// What a claim found.
/// </summary>
/// <param name="IsNew">Whether this transaction now owns a fresh key.</param>
/// <param name="Operation">The operation of the existing key; <c>null</c> when new.</param>
/// <param name="RequestHash">The fingerprint of the existing key; <c>null</c> when new.</param>
/// <param name="ResponseBody">The stored response of the existing key; <c>null</c> when new.</param>
public sealed record StoredIdempotencyKey(bool IsNew, string? Operation, byte[]? RequestHash, string? ResponseBody);
