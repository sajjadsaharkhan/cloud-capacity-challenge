namespace Arvan.Capacity.Worker;

/// <summary>
/// Settings under <c>Capacity</c> that tune the workers.
/// </summary>
public sealed class WorkerOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Capacity";

    /// <summary>
    /// How the sweeper is paced.
    /// </summary>
    public SweeperOptions Sweeper { get; init; } = new();

    /// <summary>
    /// How the relay is paced.
    /// </summary>
    public RelayOptions Relay { get; init; } = new();
}

/// <summary>
/// Pacing of the sweeper role.
/// </summary>
public sealed class SweeperOptions
{
    /// <summary>
    /// Milliseconds between iterations.
    /// </summary>
    public int IntervalMs { get; init; } = 1000;

    /// <summary>
    /// The largest number of reservations finalized in one transaction.
    /// </summary>
    public int BatchSize { get; init; } = 200;
}

/// <summary>
/// Pacing of the relay role.
/// </summary>
public sealed class RelayOptions
{
    /// <summary>
    /// Milliseconds between iterations.
    /// </summary>
    public int IntervalMs { get; init; } = 200;

    /// <summary>
    /// The largest number of events published in one transaction.
    /// </summary>
    public int BatchSize { get; init; } = 500;
}
