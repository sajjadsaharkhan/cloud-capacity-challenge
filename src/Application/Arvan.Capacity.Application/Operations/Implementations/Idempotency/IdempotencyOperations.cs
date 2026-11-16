using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Repositories;

namespace Arvan.Capacity.Application.Operations.Implementations.Idempotency;

/// <inheritdoc cref="IIdempotencyOperations"/>
public sealed class IdempotencyOperations(IIdempotencyRepository keys) : IIdempotencyOperations
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc cref="IIdempotencyOperations.ClaimAsync{T}"/>
    public async Task<IdempotencyClaim<T>> ClaimAsync<T>(
        string customerId,
        string key,
        string operation,
        object requestBody,
        CancellationToken ct) where T : class
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{operation}\n{JsonSerializer.Serialize(requestBody, Json)}"));
        var stored = await keys.ClaimAsync(customerId, key, operation, hash, ct);

        if (stored.IsNew)
            return new IdempotencyClaim<T>(IsReplay: false, Response: null);

        if (stored.Operation != operation
            || stored.RequestHash is null
            || !CryptographicOperations.FixedTimeEquals(stored.RequestHash, hash)
            || stored.ResponseBody is null)
            throw new IdempotencyKeyReusedException();

        return new IdempotencyClaim<T>(IsReplay: true, JsonSerializer.Deserialize<T>(stored.ResponseBody, Json));
    }

    /// <inheritdoc cref="IIdempotencyOperations.CompleteAsync{T}"/>
    public Task CompleteAsync<T>(string customerId, string key, Guid? reservationId, T response, CancellationToken ct)
        where T : class =>
        keys.CompleteAsync(customerId, key, reservationId, JsonSerializer.Serialize(response, Json), ct);

    /// <inheritdoc cref="IIdempotencyOperations.PurgeExpiredAsync"/>
    public Task<int> PurgeExpiredAsync(int limit, CancellationToken ct) => keys.PurgeExpiredAsync(limit, ct);
}
