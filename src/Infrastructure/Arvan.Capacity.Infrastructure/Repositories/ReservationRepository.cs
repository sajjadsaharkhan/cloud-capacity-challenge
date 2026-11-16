using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Infrastructure.Persistence;
using System.Text.Json;
using Dapper;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <inheritdoc cref="IReservationRepository"/>
internal sealed class ReservationRepository(PostgresSession session) : IReservationRepository
{
    private const string UniqueClientReference = "reservations_client_reference_live";

    /// <inheritdoc cref="IRepository{TAggregate}.AddAsync"/>
    /// <remarks>
    /// Queued, not sent: the request, its lines and its events travel with the next statement that
    /// needs an answer. A lost race on the client-reference unique index surfaces at that flush.
    /// </remarks>
    public Task AddAsync(Reservation reservation, CancellationToken ct)
    {
        session.Queue(
            """
            INSERT INTO reservations (id, customer_id, product_code, region, zone, stored_status, reason_code,
                                      on_shortage, expires_at, wait_deadline, extended, allocation_state,
                                      client_reference, version, created_at, updated_at)
            VALUES (@id, @customerId, @productCode, @region, @zone, @status, @reason,
                    @onShortage, @expiresAt, @waitDeadline, @extended, @allocationState,
                    @clientReference, @version, @createdAt, @updatedAt)
            """,
            ("id", reservation.Id), ("customerId", reservation.CustomerId), ("productCode", reservation.ProductCode),
            ("region", reservation.Region), ("zone", reservation.Zone),
            ("status", DbEnum.ToDb(reservation.StoredStatus)), ("reason", DbEnum.ToDb(reservation.ReasonCode)),
            ("onShortage", DbEnum.ToDb(reservation.OnShortage)), ("expiresAt", reservation.ExpiresAt),
            ("waitDeadline", reservation.WaitDeadline), ("extended", reservation.Extended),
            ("allocationState", DbEnum.ToDb(reservation.AllocationState)), ("clientReference", reservation.ClientReference),
            ("version", reservation.Version), ("createdAt", reservation.CreatedAt), ("updatedAt", reservation.UpdatedAt));

        foreach (var line in reservation.Lines)
            session.Queue(
                "INSERT INTO reservation_lines (reservation_id, resource_class, pool_id, quantity) VALUES (@reservationId, @resourceClass, @poolId, @quantity)",
                ("reservationId", reservation.Id), ("resourceClass", DbEnum.ToDb(line.ResourceClass)),
                ("poolId", line.PoolId), ("quantity", line.Quantity));

        QueueEvents(reservation, reservation.TakePendingEvents());
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="IReservationRepository.FindForUpdateAsync"/>
    public async Task<Reservation?> FindForUpdateAsync(Guid id, string customerId, CancellationToken ct)
    {
        var reservation = await session.QuerySingleOrDefaultAsync<Reservation>(new CommandDefinition(
            "SELECT * FROM reservations WHERE id = @id AND customer_id = @customerId FOR UPDATE",
            new { id, customerId },
            session.Transaction,
            cancellationToken: ct));

        if (reservation is not null)
            reservation.RestoreLines((await LoadLinesAsync([id], ct))[id]);

        return reservation;
    }

    /// <inheritdoc cref="IReservationRepository.UpdateAsync"/>
    /// <remarks>
    /// Queued, not sent. The version guard is checked when the queue is flushed.
    /// </remarks>
    public Task UpdateAsync(Reservation reservation, CancellationToken ct)
    {
        var events = reservation.TakePendingEvents();
        var previousVersion = events[0].Version - 1;

        session.Queue(
            """
            UPDATE reservations
            SET stored_status = @status, reason_code = @reason, zone = @zone, expires_at = @expiresAt,
                wait_deadline = @waitDeadline, extended = @extended, allocation_state = @allocationState,
                version = @version, updated_at = @updatedAt
            WHERE id = @id AND version = @previousVersion
            """,
            1, $"Reservation {reservation.Id} changed underneath a transaction that held its row lock.",
            ("id", reservation.Id), ("status", DbEnum.ToDb(reservation.StoredStatus)), ("reason", DbEnum.ToDb(reservation.ReasonCode)),
            ("zone", reservation.Zone), ("expiresAt", reservation.ExpiresAt), ("waitDeadline", reservation.WaitDeadline),
            ("extended", reservation.Extended), ("allocationState", DbEnum.ToDb(reservation.AllocationState)),
            ("version", reservation.Version), ("updatedAt", reservation.UpdatedAt), ("previousVersion", previousVersion));

        if (events.Any(e => e.Event is ReservationEventType.Promote))
            foreach (var line in reservation.Lines)
                session.Queue(
                    "UPDATE reservation_lines SET pool_id = @poolId WHERE reservation_id = @reservationId AND resource_class = @resourceClass",
                    ("poolId", line.PoolId), ("reservationId", reservation.Id), ("resourceClass", DbEnum.ToDb(line.ResourceClass)));

        QueueEvents(reservation, events);
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="IReservationRepository.ClientReferenceInUseAsync"/>
    public Task<bool> ClientReferenceInUseAsync(string customerId, string clientReference, CancellationToken ct) =>
        session.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (SELECT 1 FROM reservations
                           WHERE customer_id = @customerId AND client_reference = @clientReference
                             AND stored_status IN ('WAITING', 'HELD', 'CONFIRMED'))
            """,
            new { customerId, clientReference },
            session.Transaction,
            cancellationToken: ct));

    /// <inheritdoc cref="IReservationRepository.LockExpiredHeldAsync"/>
    public async Task<IReadOnlyList<Reservation>> LockExpiredHeldAsync(
        int limit, IReadOnlyCollection<int>? poolIds, CancellationToken ct)
    {
        var reservations = (await session.QueryAsync<Reservation>(new CommandDefinition(
            """
            SELECT r.* FROM reservations r
            WHERE r.stored_status = 'HELD' AND r.expires_at <= now()
              AND (@poolIds::int[] IS NULL OR EXISTS (SELECT 1 FROM reservation_lines l
                                                      WHERE l.reservation_id = r.id AND l.pool_id = ANY(@poolIds)))
            ORDER BY r.expires_at
            LIMIT @limit
            FOR UPDATE OF r SKIP LOCKED
            """,
            new { limit, poolIds = poolIds?.ToArray() },
            session.Transaction,
            cancellationToken: ct))).ToList();

        if (reservations.Count is 0)
            return reservations;

        var lines = await LoadLinesAsync([.. reservations.Select(r => r.Id)], ct);
        foreach (var reservation in reservations)
            reservation.RestoreLines(lines[reservation.Id]);

        return reservations;
    }

    /// <inheritdoc cref="IReservationRepository.ExpiredHeldExistsAsync"/>
    public Task<bool> ExpiredHeldExistsAsync(IReadOnlyCollection<int> poolIds, CancellationToken ct) =>
        session.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (SELECT 1 FROM reservations r
                           WHERE r.stored_status = 'HELD' AND r.expires_at <= now()
                             AND EXISTS (SELECT 1 FROM reservation_lines l
                                         WHERE l.reservation_id = r.id AND l.pool_id = ANY(@poolIds)))
            """,
            new { poolIds = poolIds.ToArray() },
            session.Transaction,
            cancellationToken: ct));

    private async Task<Dictionary<Guid, List<ReservationLine>>> LoadLinesAsync(Guid[] ids, CancellationToken ct)
    {
        var rows = await session.QueryAsync<(Guid ReservationId, string ResourceClass, int? PoolId, long Quantity)>(
            new CommandDefinition(
                """
                SELECT reservation_id AS ReservationId, resource_class AS ResourceClass, pool_id AS PoolId, quantity AS Quantity
                FROM reservation_lines WHERE reservation_id = ANY(@ids) ORDER BY resource_class
                """,
                new { ids },
                session.Transaction,
                cancellationToken: ct));

        var lines = ids.ToDictionary(id => id, _ => new List<ReservationLine>());
        foreach (var row in rows)
            lines[row.ReservationId].Add(new ReservationLine(DbEnum.FromDb<Domain.Enums.ResourceClass>(row.ResourceClass), row.Quantity, row.PoolId));

        return lines;
    }

    private void QueueEvents(Reservation reservation, IReadOnlyList<ReservationEvent> events)
    {
        foreach (var e in events)
            session.Queue(
                """
                INSERT INTO reservation_events (reservation_id, version, event, from_status, to_status,
                                                actor_kind, actor_id, reason_code, payload, at)
                VALUES (@reservationId, @version, @event, @fromStatus, @toStatus, @actorKind, @actorId, @reason, @payload::jsonb, @at)
                """,
                ("reservationId", reservation.Id), ("version", e.Version), ("event", DbEnum.ToDb(e.Event)),
                ("fromStatus", DbEnum.ToDb(e.FromStatus)), ("toStatus", DbEnum.ToDb(e.ToStatus)),
                ("actorKind", DbEnum.ToDb(e.Actor.Kind)), ("actorId", e.Actor.Id), ("reason", DbEnum.ToDb(e.ReasonCode)),
                ("payload", Envelope(reservation, e)), ("at", e.At));
    }

    // Everything but event_id, which the relay adds from the row id once it is known. The snapshot
    // of the reservation is taken at write time, so a consumer sees the state after this transition.
    private static string Envelope(Reservation reservation, ReservationEvent e) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["event_type"] = e.RoutingKey,
            ["occurred_at"] = e.At,
            ["reservation_id"] = reservation.Id,
            ["version"] = e.Version,
            ["customer_id"] = reservation.CustomerId,
            ["region"] = reservation.Region,
            ["product"] = reservation.ProductCode,
            ["from_status"] = DbEnum.ToDb(e.FromStatus),
            ["to_status"] = DbEnum.ToDb(e.ToStatus),
            ["actor"] = new { kind = DbEnum.ToDb(e.Actor.Kind), id = e.Actor.Id },
            ["reason_code"] = DbEnum.ToDb(e.ReasonCode),
            ["lines"] = reservation.Lines.Select(l => new
            {
                resource_class = DbEnum.ToDb(l.ResourceClass), quantity = l.Quantity, zone = reservation.Zone,
            }),
            ["expires_at"] = reservation.ExpiresAt,
        });

    /// <inheritdoc cref="IReservationRepository.CountOpenAsync"/>
    public async Task<OpenReservationCounts> CountOpenAsync(string customerId, string productCode, CancellationToken ct)
    {
        var (holds, waiting) = await session.QuerySingleAsync<(int Holds, int Waiting)>(new CommandDefinition(
            """
            SELECT count(*) FILTER (WHERE stored_status = 'HELD' AND expires_at > now())          AS Holds,
                   count(*) FILTER (WHERE stored_status = 'WAITING' AND wait_deadline > now())    AS Waiting
            FROM reservations
            WHERE customer_id = @customerId AND product_code = @productCode AND stored_status IN ('HELD', 'WAITING')
            """,
            new { customerId, productCode },
            session.Transaction,
            cancellationToken: ct));

        return new OpenReservationCounts(holds, waiting);
    }

    /// <inheritdoc cref="IReservationRepository.GetRecentExpiriesAsync"/>
    public async Task<RecentExpiries> GetRecentExpiriesAsync(string customerId, string productCode, CancellationToken ct)
    {
        var (count, latest) = await session.QuerySingleAsync<(int Count, DateTime? Latest)>(new CommandDefinition(
            """
            SELECT count(*) AS Count, max(updated_at) AS Latest
            FROM reservations
            WHERE customer_id = @customerId AND product_code = @productCode
              AND stored_status = 'EXPIRED' AND updated_at > now() - interval '1 hour'
            """,
            new { customerId, productCode },
            session.Transaction,
            cancellationToken: ct));

        return new RecentExpiries(count, latest is null ? null : new DateTimeOffset(DateTime.SpecifyKind(latest.Value, DateTimeKind.Utc)));
    }

    /// <inheritdoc cref="IReservationRepository.LockWaitingAsync"/>
    public Task<IReadOnlyList<Reservation>> LockWaitingAsync(int limit, CancellationToken ct) =>
        LockAsync(
            "WHERE r.stored_status = 'WAITING' AND r.wait_deadline > now() ORDER BY r.created_at LIMIT @limit",
            new { limit }, ct);

    /// <inheritdoc cref="IReservationRepository.LockTimedOutWaitingAsync"/>
    public Task<IReadOnlyList<Reservation>> LockTimedOutWaitingAsync(int limit, CancellationToken ct) =>
        LockAsync(
            "WHERE r.stored_status = 'WAITING' AND r.wait_deadline <= now() ORDER BY r.wait_deadline LIMIT @limit",
            new { limit }, ct);

    /// <inheritdoc cref="IReservationRepository.LockOpenAsync"/>
    public Task<IReadOnlyList<Reservation>> LockOpenAsync(string customerId, int limit, CancellationToken ct) =>
        LockAsync(
            """
            WHERE r.customer_id = @customerId
              AND ((r.stored_status = 'HELD' AND r.expires_at > now()) OR (r.stored_status = 'WAITING' AND r.wait_deadline > now()))
            ORDER BY r.created_at LIMIT @limit
            """,
            new { customerId, limit }, ct);

    private async Task<IReadOnlyList<Reservation>> LockAsync(string tail, object parameters, CancellationToken ct)
    {
        var reservations = (await session.QueryAsync<Reservation>(new CommandDefinition(
            $"SELECT r.* FROM reservations r {tail} FOR UPDATE OF r SKIP LOCKED",
            parameters,
            session.Transaction,
            cancellationToken: ct))).ToList();

        if (reservations.Count is 0)
            return reservations;

        var lines = await LoadLinesAsync([.. reservations.Select(r => r.Id)], ct);
        foreach (var reservation in reservations)
            reservation.RestoreLines(lines[reservation.Id]);

        return reservations;
    }
}
