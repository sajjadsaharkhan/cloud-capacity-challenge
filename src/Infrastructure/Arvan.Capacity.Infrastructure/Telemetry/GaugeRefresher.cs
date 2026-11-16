using System.Diagnostics.Metrics;
using Arvan.Capacity.Application.Telemetry;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Arvan.Capacity.Infrastructure.Telemetry;

/// <summary>
/// Keeps the database-backed gauges fresh. A gauge callback must return at once, so this reads the
/// numbers every few seconds and the callbacks only report the last values read.
/// </summary>
internal sealed class GaugeRefresher : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<GaugeRefresher> _logger;

    private volatile Snapshot _snapshot = new([], [], [], [], 0, 0, 0);

    private sealed record Snapshot(
        IReadOnlyList<(string Region, long Count)> Unsettled,
        IReadOnlyList<(string Region, string Product, long Count)> Waiting,
        IReadOnlyList<(int Pool, string Class, long Free)> Free,
        IReadOnlyList<(int Pool, string Class, long PerSecond)> HoldRate,
        long OutboxBacklog,
        long OutboxOldestAge,
        long Drift);

    public GaugeRefresher(NpgsqlDataSource dataSource, ILogger<GaugeRefresher> logger)
    {
        _dataSource = dataSource;
        _logger = logger;

        CapacityTelemetry.Gauge("capacity_unsettled_holds", () =>
            _snapshot.Unsettled.Select(u => new Measurement<long>(u.Count, Tag("region", u.Region))));
        CapacityTelemetry.Gauge("capacity_waiting_requests", () =>
            _snapshot.Waiting.Select(w => new Measurement<long>(w.Count, Tag("region", w.Region), Tag("product", w.Product))));
        CapacityTelemetry.Gauge("capacity_free_units", () =>
            _snapshot.Free.Select(f => new Measurement<long>(f.Free, Tag("pool", f.Pool), Tag("class", f.Class))));
        CapacityTelemetry.Gauge("capacity_holds_per_row_rate", () =>
            _snapshot.HoldRate.Select(h => new Measurement<long>(h.PerSecond, Tag("pool", h.Pool), Tag("class", h.Class))));
        CapacityTelemetry.Gauge("capacity_outbox_backlog", () => [new Measurement<long>(_snapshot.OutboxBacklog)]);
        CapacityTelemetry.Gauge("capacity_outbox_oldest_age_seconds", () => [new Measurement<long>(_snapshot.OutboxOldestAge)]);
        CapacityTelemetry.Gauge("capacity_ledger_drift_rows", () => [new Measurement<long>(_snapshot.Drift)]);
    }

    private static KeyValuePair<string, object?> Tag(string name, object value) => new(name, value);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _snapshot = await ReadAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogWarning(e, "Could not refresh the gauges");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task<Snapshot> ReadAsync(CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        var unsettled = (await connection.QueryAsync<(string Region, long Count)>(new CommandDefinition(
            "SELECT region AS Region, count(*) AS Count FROM reservations WHERE stored_status = 'HELD' AND expires_at <= now() GROUP BY region",
            cancellationToken: ct))).ToList();

        var waiting = (await connection.QueryAsync<(string Region, string Product, long Count)>(new CommandDefinition(
            "SELECT region AS Region, product_code AS Product, count(*) AS Count FROM reservations WHERE stored_status = 'WAITING' GROUP BY region, product_code",
            cancellationToken: ct))).ToList();

        var free = (await connection.QueryAsync<(int Pool, string Class, long Free)>(new CommandDefinition(
            """
            SELECT i.pool_id AS Pool, i.resource_class AS Class,
                   GREATEST(0, floor((i.total - i.reserved) * i.allocation_ratio)::bigint - i.held - i.used) AS Free
            FROM inventories i JOIN pools p ON p.id = i.pool_id WHERE p.admin_state = 'AVAILABLE'
            """,
            cancellationToken: ct))).ToList();

        var rate = (await connection.QueryAsync<(int Pool, string Class, long PerSecond)>(new CommandDefinition(
            """
            SELECT pool_id AS Pool, resource_class AS Class, (count(*) / 10) AS PerSecond
            FROM ledger_entries WHERE entry_type = 'HOLD' AND at > now() - interval '10 seconds' GROUP BY pool_id, resource_class
            """,
            cancellationToken: ct))).ToList();

        var outbox = await connection.QuerySingleAsync<(long Backlog, long Oldest)>(new CommandDefinition(
            """
            SELECT count(*) AS Backlog, COALESCE(EXTRACT(EPOCH FROM now() - min(at))::bigint, 0) AS Oldest
            FROM reservation_events WHERE published_at IS NULL
            """,
            cancellationToken: ct));

        var drift = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT drift_rows FROM reconciliation_reports ORDER BY id DESC LIMIT 1", cancellationToken: ct)) ?? 0;

        return new Snapshot(unsettled, waiting, free, rate, outbox.Backlog, outbox.Oldest, drift);
    }
}
