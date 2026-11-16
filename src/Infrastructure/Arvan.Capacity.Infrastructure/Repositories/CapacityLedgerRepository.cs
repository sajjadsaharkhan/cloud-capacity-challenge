using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <inheritdoc cref="ICapacityLedgerRepository"/>
/// <remarks>
/// Every write here is queued and sent in as few round trips as the decisions allow. A hold sends
/// two batches: first everything that does not touch the contended row (quota, ledger, and the
/// request the caller queued), then the conditional inventory <c>UPDATE</c> alone. The row lock
/// therefore lasts for that last batch and the commit.
/// </remarks>
internal sealed class CapacityLedgerRepository(PostgresSession session, IOptions<CapacityOptions> options)
    : ICapacityLedgerRepository
{
    // The bound mirrors InventorySnapshot.Sellable; an integration test keeps the two in agreement.
    private const string HoldSql =
        """
        UPDATE inventories AS i SET held = i.held + @quantity
        FROM pools AS p
        WHERE i.pool_id = @poolId AND i.resource_class = @resourceClass
          AND p.id = i.pool_id AND p.admin_state = 'AVAILABLE'
          AND i.held + i.used + @quantity <= floor((i.total - i.reserved) * i.allocation_ratio)
        """;

    /// <inheritdoc cref="ICapacityLedgerRepository.GetSnapshotsAsync"/>
    public async Task<IReadOnlyList<InventorySnapshot>> GetSnapshotsAsync(
        string region, IReadOnlyCollection<ResourceClass> classes, CancellationToken ct) =>
        await InventorySnapshots.QueryAsync(await session.ReadConnectionAsync(ct), session.Transaction, region, classes, ct);

    /// <inheritdoc cref="ICapacityLedgerRepository.TryHoldAsync"/>
    public async Task<HoldResult> TryHoldAsync(LedgerHold request, DateTimeOffset now, CancellationToken ct)
    {
        var lines = request.Lines.OrderBy(l => l.ResourceClass).ToList();

        // Batch one: quota rows (locked before inventory rows, the global order) and the ledger,
        // together with whatever the caller queued before asking, such as the request itself. A
        // missing quota row is created with the class's default; it never means unlimited.
        var charges = new List<PostgresSession.PendingCommand>();
        foreach (var line in lines)
            session.Queue(
                """
                INSERT INTO quotas (customer_id, resource_class, region, quota_limit)
                VALUES (@customerId, @resourceClass, @region, @limit)
                ON CONFLICT DO NOTHING
                """,
                ("customerId", request.CustomerId), ("resourceClass", DbEnum.ToDb(line.ResourceClass)),
                ("region", request.Region), ("limit", options.Value.DefaultQuotaOf(line.ResourceClass)));

        foreach (var line in lines)
            charges.Add(session.Queue(
                """
                UPDATE quotas SET held = held + @quantity
                WHERE customer_id = @customerId AND resource_class = @resourceClass AND region = @region
                  AND held + used + @quantity <= quota_limit
                """,
                ("customerId", request.CustomerId), ("resourceClass", DbEnum.ToDb(line.ResourceClass)),
                ("region", request.Region), ("quantity", line.Quantity)));

        // Written before the contended row is touched; if the hold fails, the caller's savepoint
        // takes them back.
        QueueLedger(
            request.ReservationId, request.CustomerId, request.Region,
            lines.Select(l => (l.PoolId, l.ResourceClass, Held: l.Quantity, Used: 0L)), LedgerEntryType.Hold, now);

        await session.FlushAsync(ct);

        if (charges.Any(c => c.RecordsAffected is not 1))
            return HoldResult.QuotaExceeded;

        // Under the quota row's lock, so two parallel requests of one customer cannot both pass.
        if (request.MaxPoolSharePct < 100)
            foreach (var line in lines.OrderBy(l => l.PoolId))
                if (await ExceedsPoolShareAsync(request, line, ct))
                    return HoldResult.PoolShareExceeded;

        // Batch two, alone: the statements run in pool-and-class order, the global lock order, so
        // two holds can never wait on each other's rows in a cycle. This is the only time the
        // contended inventory rows are locked; the commit follows at once.
        var holds = lines
            .OrderBy(l => l.PoolId).ThenBy(l => l.ResourceClass)
            .Select(l => (Line: l, Command: session.Queue(
                HoldSql, ("poolId", l.PoolId), ("resourceClass", DbEnum.ToDb(l.ResourceClass)), ("quantity", l.Quantity))))
            .ToList();

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await session.FlushAsync(ct);
        var waited = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;

        foreach (var (line, _) in holds)
            Application.Telemetry.CapacityTelemetry.InventoryUpdateLockWait.Record(
                waited, new("pool", line.PoolId), new("class", DbEnum.ToDb(line.ResourceClass)));

        return holds.All(h => h.Command.RecordsAffected is 1) ? HoldResult.Held : HoldResult.CapacityShort;
    }

    /// <inheritdoc cref="ICapacityLedgerRepository.GetEarliestHoldExpiryAsync"/>
    public async Task<DateTimeOffset?> GetEarliestHoldExpiryAsync(IReadOnlyCollection<int> poolIds, CancellationToken ct)
    {
        // Only leases ending within the next minute can matter: the caller clamps its Retry-After to
        // 1-60 s and answers 60 when this finds nothing. Bounding the range keeps the cost small
        // however many holds sit on the pool, and however few of them are on these pools.
        var earliest = await session.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            """
            SELECT min(r.expires_at) FROM reservations r
            WHERE r.stored_status = 'HELD' AND r.expires_at <= now() + interval '60 seconds'
              AND EXISTS (SELECT 1 FROM reservation_lines l WHERE l.reservation_id = r.id AND l.pool_id = ANY(@poolIds))
            """,
            new { poolIds = poolIds.ToArray() },
            session.Transaction,
            cancellationToken: ct));

        return earliest is null ? null : new DateTimeOffset(DateTime.SpecifyKind(earliest.Value, DateTimeKind.Utc));
    }

    /// <inheritdoc cref="ICapacityLedgerRepository.CommitAsync"/>
    public Task CommitAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct)
    {
        var lines = Sorted(reservation);

        QueueQuota(PerQuota([reservation]), "held = held - @quantity, used = used + @quantity");
        QueueInventory(lines.Select(l => (l.PoolId!.Value, l.ResourceClass, l.Quantity)), "held = held - @quantity, used = used + @quantity");
        QueueLedger(
            reservation.Id, reservation.CustomerId, reservation.Region,
            lines.Select(l => (l.PoolId!.Value, l.ResourceClass, Held: -l.Quantity, Used: l.Quantity)), LedgerEntryType.Commit, now);

        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ICapacityLedgerRepository.ReturnAsync"/>
    public Task ReturnAsync(IReadOnlyCollection<Reservation> reservations, DateTimeOffset now, CancellationToken ct)
    {
        // Summed per row, so 200 reservations touch only a handful of quota and inventory rows.
        QueueQuota(PerQuota(reservations), "held = held - @quantity");

        QueueInventory(
            reservations
                .SelectMany(r => r.Lines)
                .GroupBy(l => (PoolId: l.PoolId!.Value, l.ResourceClass))
                .Select(g => (g.Key.PoolId, g.Key.ResourceClass, Quantity: g.Sum(l => l.Quantity))),
            "held = held - @quantity");

        foreach (var reservation in reservations)
            QueueLedger(
                reservation.Id, reservation.CustomerId, reservation.Region,
                Sorted(reservation).Select(l => (l.PoolId!.Value, l.ResourceClass, Held: -l.Quantity, Used: 0L)),
                LedgerEntryType.Return, now);

        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ICapacityLedgerRepository.ReleaseAsync"/>
    public Task ReleaseAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct)
    {
        var lines = Sorted(reservation);

        QueueQuota(PerQuota([reservation]), "used = used - @quantity");
        QueueInventory(lines.Select(l => (l.PoolId!.Value, l.ResourceClass, l.Quantity)), "used = used - @quantity");
        QueueLedger(
            reservation.Id, reservation.CustomerId, reservation.Region,
            lines.Select(l => (l.PoolId!.Value, l.ResourceClass, Held: 0L, Used: -l.Quantity)), LedgerEntryType.Release, now);

        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ICapacityLedgerRepository.AdjustAsync"/>
    public async Task AdjustAsync(LedgerAdjustment adjustment, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var inventory = adjustment.PoolId is not null;

            var updated = await session.ExecuteAsync(new CommandDefinition(
                inventory
                    ? "UPDATE inventories SET held = held + @deltaHeld, used = used + @deltaUsed WHERE pool_id = @poolId AND resource_class = @resourceClass"
                    : "UPDATE quotas SET held = held + @deltaHeld, used = used + @deltaUsed WHERE customer_id = @customerId AND region = @region AND resource_class = @resourceClass",
                new
                {
                    adjustment.PoolId, adjustment.CustomerId, adjustment.Region,
                    resourceClass = DbEnum.ToDb(adjustment.ResourceClass), adjustment.DeltaHeld, adjustment.DeltaUsed,
                },
                session.Transaction,
                cancellationToken: ct));

            if (updated is not 1)
                throw new UnprocessableException("ledger.target_not_found", "The counter to adjust does not exist.");

            await session.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO ledger_entries (pool_id, resource_class, customer_id, region, entry_type, delta_held, delta_used, actor_id, reason, at)
                VALUES (@PoolId, @resourceClass, @CustomerId,
                        COALESCE(@Region, (SELECT region FROM pools WHERE id = @PoolId)),
                        'ADJUST', @DeltaHeld, @DeltaUsed, @ActorId, @Reason, @now)
                """,
                new
                {
                    adjustment.PoolId, adjustment.CustomerId, adjustment.Region, resourceClass = DbEnum.ToDb(adjustment.ResourceClass),
                    adjustment.DeltaHeld, adjustment.DeltaUsed, adjustment.ActorId, adjustment.Reason, now,
                },
                session.Transaction,
                cancellationToken: ct));
        }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.CheckViolation)
        {
            throw new UnprocessableException(
                "ledger.adjustment_breaks_constraint", "The adjustment would break a counter constraint.");
        }
    }

    private async Task<bool> ExceedsPoolShareAsync(LedgerHold request, LineAllocation line, CancellationToken ct)
    {
        var (existing, sellable) = await session.QuerySingleAsync<(long Existing, long Sellable)>(new CommandDefinition(
            """
            SELECT COALESCE((SELECT sum(l.quantity)
                             FROM reservation_lines l JOIN reservations r ON r.id = l.reservation_id
                             WHERE r.customer_id = @customerId AND l.pool_id = @poolId AND l.resource_class = @resourceClass
                               AND r.id <> @reservationId
                               AND (r.stored_status = 'HELD' OR (r.stored_status = 'CONFIRMED' AND r.allocation_state = 'ACTIVE'))), 0) AS Existing,
                   (SELECT floor((total - reserved) * allocation_ratio)::bigint FROM inventories
                    WHERE pool_id = @poolId AND resource_class = @resourceClass) AS Sellable
            """,
            new { customerId = request.CustomerId, poolId = line.PoolId, resourceClass = DbEnum.ToDb(line.ResourceClass), request.ReservationId },
            session.Transaction,
            cancellationToken: ct));

        return (existing + line.Quantity) * 100 > sellable * request.MaxPoolSharePct;
    }

    private static IEnumerable<(string CustomerId, string Region, ResourceClass ResourceClass, long Quantity)> PerQuota(
        IEnumerable<Reservation> reservations) =>
        reservations
            .SelectMany(r => r.Lines.Select(l => (r.CustomerId, r.Region, l.ResourceClass, l.Quantity)))
            .GroupBy(x => (x.CustomerId, x.Region, x.ResourceClass))
            .Select(g => (g.Key.CustomerId, g.Key.Region, g.Key.ResourceClass, Quantity: g.Sum(x => x.Quantity)))
            .OrderBy(x => x.CustomerId, StringComparer.Ordinal).ThenBy(x => x.Region, StringComparer.Ordinal).ThenBy(x => x.ResourceClass);

    private void QueueQuota(
        IEnumerable<(string CustomerId, string Region, ResourceClass ResourceClass, long Quantity)> changes, string set)
    {
        foreach (var change in changes)
            session.Queue(
                $"UPDATE quotas SET {set} WHERE customer_id = @customerId AND region = @region AND resource_class = @resourceClass",
                1, "A reservation named a quota row that does not exist.",
                ("customerId", change.CustomerId), ("region", change.Region),
                ("resourceClass", DbEnum.ToDb(change.ResourceClass)), ("quantity", change.Quantity));
    }

    /// <summary>
    /// Queues an unconditional counter change per inventory, in pool-and-class order. The table's
    /// CHECK constraints catch a double application.
    /// </summary>
    private void QueueInventory(IEnumerable<(int PoolId, ResourceClass ResourceClass, long Quantity)> changes, string set)
    {
        foreach (var change in changes.OrderBy(c => c.PoolId).ThenBy(c => c.ResourceClass))
            session.Queue(
                $"UPDATE inventories SET {set} WHERE pool_id = @poolId AND resource_class = @resourceClass",
                1, "A reservation line named an inventory that does not exist.",
                ("poolId", change.PoolId), ("resourceClass", DbEnum.ToDb(change.ResourceClass)), ("quantity", change.Quantity));
    }

    private static List<ReservationLine> Sorted(Reservation reservation) =>
        [.. reservation.Lines.OrderBy(l => l.PoolId).ThenBy(l => l.ResourceClass)];

    private void QueueLedger(
        Guid reservationId,
        string customerId,
        string region,
        IEnumerable<(int PoolId, ResourceClass ResourceClass, long Held, long Used)> deltas,
        LedgerEntryType type,
        DateTimeOffset now)
    {
        foreach (var d in deltas)
            session.Queue(
                """
                INSERT INTO ledger_entries (pool_id, resource_class, reservation_id, customer_id, region, entry_type,
                                            delta_held, delta_used, at)
                VALUES (@poolId, @resourceClass, @reservationId, @customerId, @region, @entryType, @deltaHeld, @deltaUsed, @at)
                """,
                ("poolId", d.PoolId), ("resourceClass", DbEnum.ToDb(d.ResourceClass)), ("reservationId", reservationId),
                ("customerId", customerId), ("region", region), ("entryType", DbEnum.ToDb(type)),
                ("deltaHeld", d.Held), ("deltaUsed", d.Used), ("at", now));
    }
}
