namespace Arvan.Capacity.Infrastructure.Persistence;

/// <summary>
/// Settings under <c>Database</c> in configuration.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// How long one statement may run before PostgreSQL cancels it. A transaction cannot outlive
    /// this by much, which is what keeps its start time a fair stand-in for "now".
    /// </summary>
    public TimeSpan StatementTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a statement may wait for a row lock before giving up, so a hot row sheds load
    /// instead of queueing without bound.
    /// </summary>
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How many times a transaction is re-run after a deadlock or serialization failure.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Whether the host applies pending migrations at startup.
    /// </summary>
    public bool Migrate { get; init; }

    /// <summary>
    /// Whether the host loads the demo catalog into an empty database at startup.
    /// </summary>
    public bool SeedDemoData { get; init; }
}
