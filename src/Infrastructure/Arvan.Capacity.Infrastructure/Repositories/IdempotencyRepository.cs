using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Constants;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <inheritdoc cref="IIdempotencyRepository"/>
internal sealed class IdempotencyRepository(PostgresSession session) : IIdempotencyRepository
{
    /// <inheritdoc cref="IIdempotencyRepository.ClaimAsync"/>
    public async Task<StoredIdempotencyKey> ClaimAsync(
        string customerId, string key, string operation, byte[] requestHash, CancellationToken ct)
    {
        // A concurrent claim of the same key blocks here until the first transaction ends; if it
        // committed, the insert finds a conflict and we read what it stored, if it rolled back, we
        // own the key. No "in progress" state is ever visible.
        var inserted = await session.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            INSERT INTO idempotency_keys (customer_id, idem_key, operation, request_hash, expires_at)
            VALUES (@customerId, @key, @operation, @requestHash, now() + @retention)
            ON CONFLICT (customer_id, idem_key) DO NOTHING
            RETURNING 1
            """,
            new { customerId, key, operation, requestHash, retention = ReservationConstant.IdempotencyRetention },
            session.Transaction,
            cancellationToken: ct));

        if (inserted is not null)
            return new StoredIdempotencyKey(IsNew: true, null, null, null);

        var stored = await session.QuerySingleAsync<(string Operation, byte[] RequestHash, string? ResponseBody)>(
            new CommandDefinition(
                """
                SELECT operation AS Operation, request_hash AS RequestHash, response_body::text AS ResponseBody
                FROM idempotency_keys WHERE customer_id = @customerId AND idem_key = @key
                """,
                new { customerId, key },
                session.Transaction,
                cancellationToken: ct));

        return new StoredIdempotencyKey(IsNew: false, stored.Operation, stored.RequestHash, stored.ResponseBody);
    }

    /// <inheritdoc cref="IIdempotencyRepository.CompleteAsync"/>
    /// <remarks>
    /// Queued, not sent: the response travels with the writes of the same call.
    /// </remarks>
    public Task CompleteAsync(string customerId, string key, Guid? reservationId, string responseBody, CancellationToken ct)
    {
        session.Queue(
            """
            UPDATE idempotency_keys SET reservation_id = @reservationId, response_body = @responseBody::jsonb
            WHERE customer_id = @customerId AND idem_key = @key
            """,
            ("reservationId", reservationId), ("responseBody", responseBody), ("customerId", customerId), ("key", key));

        return Task.CompletedTask;
    }

    /// <inheritdoc cref="IIdempotencyRepository.PurgeExpiredAsync"/>
    public Task<int> PurgeExpiredAsync(int limit, CancellationToken ct) =>
        session.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM idempotency_keys
            WHERE (customer_id, idem_key) IN (SELECT customer_id, idem_key FROM idempotency_keys
                                              WHERE expires_at < now() LIMIT @limit)
            """,
            new { limit },
            session.Transaction,
            cancellationToken: ct));
}
