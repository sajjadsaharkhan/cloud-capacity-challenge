using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <inheritdoc cref="IOutboxRepository"/>
internal sealed class OutboxRepository(PostgresSession session) : IOutboxRepository
{

    /// <inheritdoc cref="IOutboxRepository.LockUnpublishedAsync"/>
    public async Task<IReadOnlyList<OutboxEvent>> LockUnpublishedAsync(int limit, CancellationToken ct)
    {
        var rows = await session.QueryAsync<(long Id, string Payload)>(new CommandDefinition(
            """
            SELECT id AS Id, payload::text AS Payload
            FROM reservation_events
            WHERE published_at IS NULL
            ORDER BY id
            LIMIT @limit
            FOR UPDATE SKIP LOCKED
            """,
            new { limit },
            session.Transaction,
            cancellationToken: ct));

        // The routing key is the envelope's event type, written with the row.
        return
        [
            .. rows.Select(r => new OutboxEvent(
                r.Id,
                System.Text.Json.JsonDocument.Parse(r.Payload).RootElement.GetProperty("event_type").GetString()!,
                r.Payload)),
        ];
    }

    /// <inheritdoc cref="IOutboxRepository.MarkPublishedAsync"/>
    public Task MarkPublishedAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
        session.ExecuteAsync(new CommandDefinition(
            "UPDATE reservation_events SET published_at = now() WHERE id = ANY(@ids)",
            new { ids = ids.ToArray() },
            session.Transaction,
            cancellationToken: ct));
}
