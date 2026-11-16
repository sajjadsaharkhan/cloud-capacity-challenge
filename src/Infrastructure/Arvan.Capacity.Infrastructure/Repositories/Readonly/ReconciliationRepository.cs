using System.Text.Json;
using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace Arvan.Capacity.Infrastructure.Repositories.Readonly;

/// <inheritdoc cref="IReconciliationRepository"/>
internal sealed class ReconciliationRepository(NpgsqlDataSource dataSource) : IReconciliationRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc cref="IReconciliationRepository.ComputeAsync"/>
    public async Task<ReconciliationReport> ComputeAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        // The report scans whole tables, so it gets a longer timeout than a request does, scoped to
        // this transaction so it cannot leak into the pooled connection.
        await using var tx = await connection.BeginTransactionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("SET LOCAL statement_timeout = '60s'", transaction: tx, cancellationToken: ct));
        var drift = new List<DriftRow>();

        foreach (var r in await connection.QueryAsync<(int PoolId, string Class, long Held, long Used, long LedgerHeld, long LedgerUsed)>(
            new CommandDefinition(
                """
                SELECT i.pool_id AS PoolId, i.resource_class AS Class, i.held AS Held, i.used AS Used,
                       COALESCE(sum(l.delta_held), 0)::bigint AS LedgerHeld, COALESCE(sum(l.delta_used), 0)::bigint AS LedgerUsed
                FROM inventories i
                LEFT JOIN ledger_entries l ON l.pool_id = i.pool_id AND l.resource_class = i.resource_class
                GROUP BY i.pool_id, i.resource_class, i.held, i.used
                """,
                transaction: tx, cancellationToken: ct)))
        {
            Add(drift, "inventory_vs_ledger", $"pool {r.PoolId} {r.Class}", "held", r.LedgerHeld, r.Held);
            Add(drift, "inventory_vs_ledger", $"pool {r.PoolId} {r.Class}", "used", r.LedgerUsed, r.Used);
        }

        foreach (var r in await connection.QueryAsync<(string Customer, string Class, string Region, long Held, long Used, long LedgerHeld, long LedgerUsed)>(
            new CommandDefinition(
                """
                SELECT q.customer_id AS Customer, q.resource_class AS Class, q.region AS Region, q.held AS Held, q.used AS Used,
                       COALESCE(sum(l.delta_held), 0)::bigint AS LedgerHeld, COALESCE(sum(l.delta_used), 0)::bigint AS LedgerUsed
                FROM quotas q
                LEFT JOIN ledger_entries l ON l.customer_id = q.customer_id AND l.resource_class = q.resource_class AND l.region = q.region
                GROUP BY q.customer_id, q.resource_class, q.region, q.held, q.used
                """,
                transaction: tx, cancellationToken: ct)))
        {
            Add(drift, "quota_vs_ledger", $"{r.Customer} {r.Class} {r.Region}", "held", r.LedgerHeld, r.Held);
            Add(drift, "quota_vs_ledger", $"{r.Customer} {r.Class} {r.Region}", "used", r.LedgerUsed, r.Used);
        }

        foreach (var r in await connection.QueryAsync<(int PoolId, string Class, long Held, long Used, long ExpectedHeld, long ExpectedUsed)>(
            new CommandDefinition(
                """
                SELECT i.pool_id AS PoolId, i.resource_class AS Class, i.held AS Held, i.used AS Used,
                       COALESCE((SELECT sum(l.quantity) FROM reservation_lines l JOIN reservations r ON r.id = l.reservation_id
                                 WHERE l.pool_id = i.pool_id AND l.resource_class = i.resource_class
                                   AND r.stored_status = 'HELD'), 0)::bigint AS ExpectedHeld,
                       COALESCE((SELECT sum(l.quantity) FROM reservation_lines l JOIN reservations r ON r.id = l.reservation_id
                                 WHERE l.pool_id = i.pool_id AND l.resource_class = i.resource_class
                                   AND r.stored_status = 'CONFIRMED' AND r.allocation_state = 'ACTIVE'), 0)::bigint AS ExpectedUsed
                FROM inventories i
                """,
                transaction: tx, cancellationToken: ct)))
        {
            Add(drift, "inventory_vs_reservations", $"pool {r.PoolId} {r.Class}", "held", r.ExpectedHeld, r.Held);
            Add(drift, "inventory_vs_reservations", $"pool {r.PoolId} {r.Class}", "used", r.ExpectedUsed, r.Used);
        }

        var backlog = await connection.QuerySingleAsync<(DateTime Now, long Holds, long Waiting, long Outbox, long OldestAge)>(
            new CommandDefinition(
                """
                SELECT now() AS Now,
                       (SELECT count(*) FROM reservations WHERE stored_status = 'HELD' AND expires_at <= now()) AS Holds,
                       (SELECT count(*) FROM reservations WHERE stored_status = 'WAITING' AND wait_deadline <= now()) AS Waiting,
                       (SELECT count(*) FROM reservation_events WHERE published_at IS NULL) AS Outbox,
                       COALESCE((SELECT EXTRACT(EPOCH FROM now() - min(at))::bigint FROM reservation_events WHERE published_at IS NULL), 0) AS OldestAge
                """,
                transaction: tx, cancellationToken: ct));

        return new ReconciliationReport(
            new DateTimeOffset(DateTime.SpecifyKind(backlog.Now, DateTimeKind.Utc)),
            drift, backlog.Holds, backlog.Waiting, backlog.Outbox, backlog.OldestAge);
    }

    /// <inheritdoc cref="IReconciliationRepository.SaveAsync"/>
    public async Task SaveAsync(ReconciliationReport report, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO reconciliation_reports (at, drift_rows, report) VALUES (@at, @driftRows, @report::jsonb)",
            new { at = report.GeneratedAt, driftRows = report.DriftRows, report = JsonSerializer.Serialize(report, Json) },
            cancellationToken: ct));
    }

    /// <inheritdoc cref="IReconciliationRepository.GetLatestAsync"/>
    public async Task<ReconciliationReport?> GetLatestAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var json = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT report::text FROM reconciliation_reports ORDER BY id DESC LIMIT 1", cancellationToken: ct));

        return json is null ? null : JsonSerializer.Deserialize<ReconciliationReport>(json, Json);
    }

    private static void Add(List<DriftRow> drift, string check, string key, string counter, long expected, long actual)
    {
        if (expected != actual)
            drift.Add(new DriftRow(check, key, counter, expected, actual));
    }
}
