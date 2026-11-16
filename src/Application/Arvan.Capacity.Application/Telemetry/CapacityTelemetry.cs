using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Arvan.Capacity.Application.Telemetry;

/// <summary>
/// The service's traces and metrics, in the BCL's <c>System.Diagnostics</c> types so Application
/// needs no telemetry library: the hosts subscribe to the names below.
/// </summary>
public static class CapacityTelemetry
{
    /// <summary>
    /// The name of the activity source and of the meter.
    /// </summary>
    public const string Name = "Arvan.Capacity";

    /// <summary>
    /// One span per UseCase, with <c>reservation.id</c>, <c>customer.id</c>, <c>region</c>, <c>outcome</c> and <c>code</c>.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);

    private static readonly Meter Meter = new(Name);

    /// <summary>
    /// Submit outcomes by region, product, outcome (<c>held</c>, <c>waiting</c>, <c>rejected</c>) and code.
    /// </summary>
    public static readonly Counter<long> Holds = Meter.CreateCounter<long>("capacity_holds_total");

    /// <summary>
    /// How long a submit took, by region and product.
    /// </summary>
    public static readonly Histogram<double> HoldDuration =
        Meter.CreateHistogram<double>("capacity_hold_duration_seconds", unit: "s");

    /// <summary>
    /// State transitions by event and outcome.
    /// </summary>
    public static readonly Counter<long> Transitions = Meter.CreateCounter<long>("capacity_transitions_total");

    /// <summary>
    /// How long past its deadline a hold was when the sweeper stored its end, by region.
    /// </summary>
    public static readonly Histogram<double> ExpiryLag =
        Meter.CreateHistogram<double>("capacity_expiry_lag_seconds", unit: "s");

    /// <summary>
    /// Idempotent replays by operation.
    /// </summary>
    public static readonly Counter<long> IdempotentReplays = Meter.CreateCounter<long>("capacity_idempotent_replays_total");

    /// <summary>
    /// Relay publish failures.
    /// </summary>
    public static readonly Counter<long> RelayPublishFailures =
        Meter.CreateCounter<long>("capacity_relay_publish_failures_total");

    /// <summary>
    /// How long the inventory counter update of a hold took, which is mostly waiting for the row
    /// lock; the signal that a pool has become a hot row.
    /// </summary>
    public static readonly Histogram<double> InventoryUpdateLockWait =
        Meter.CreateHistogram<double>("capacity_inventory_update_lock_wait_seconds", unit: "s");

    /// <summary>
    /// How long a transaction waited for a database connection.
    /// </summary>
    public static readonly Histogram<double> DbPoolWait =
        Meter.CreateHistogram<double>("capacity_db_pool_wait_seconds", unit: "s");

    /// <summary>
    /// Network round trips between the service and PostgreSQL per transaction, <c>BEGIN</c> and
    /// <c>COMMIT</c> included. The number the hold's latency is mostly made of.
    /// </summary>
    public static readonly Histogram<int> TransactionRoundTrips =
        Meter.CreateHistogram<int>("capacity_transaction_round_trips");

    /// <summary>
    /// How long each phase of a hold took, by phase name.
    /// </summary>
    public static readonly Histogram<double> HoldPhaseDuration =
        Meter.CreateHistogram<double>("capacity_hold_phase_duration_seconds", unit: "s");

    /// <summary>
    /// Times one phase of a UseCase: a child span, and a sample of
    /// <see cref="HoldPhaseDuration"/> when disposed.
    /// </summary>
    /// <param name="phase">The phase name, such as <c>claim</c> or <c>write</c>.</param>
    public static PhaseTimer Phase(string phase) => new(phase);

    /// <summary>
    /// Times one phase; dispose to record it.
    /// </summary>
    public readonly struct PhaseTimer : IDisposable
    {
        private readonly string _phase;
        private readonly long _started;
        private readonly Activity? _activity;

        internal PhaseTimer(string phase)
        {
            _phase = phase;
            _started = Stopwatch.GetTimestamp();
            _activity = Source.StartActivity($"hold.{phase}");
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            HoldPhaseDuration.Record(Stopwatch.GetElapsedTime(_started).TotalSeconds, new KeyValuePair<string, object?>("phase", _phase));
            _activity?.Dispose();
        }
    }

    /// <summary>
    /// Registers a gauge whose value a host refreshes from the database.
    /// </summary>
    /// <param name="name">The metric name.</param>
    /// <param name="observe">Returns the current measurements.</param>
    public static ObservableGauge<long> Gauge(string name, Func<IEnumerable<Measurement<long>>> observe) =>
        Meter.CreateObservableGauge(name, observe);
}
