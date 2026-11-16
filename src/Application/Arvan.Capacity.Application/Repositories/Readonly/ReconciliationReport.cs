namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// The result of comparing counters with the ledger and with the reservations. Read-only: a drift is
/// corrected with a ledger adjustment, never automatically.
/// </summary>
/// <param name="GeneratedAt">The database time the report was computed.</param>
/// <param name="Drift">Every counter that disagrees with its ledger or its reservations.</param>
/// <param name="UnsettledHolds">Holds past their lease but not yet stored as expired.</param>
/// <param name="UnsettledWaiting">Waiting requests past their deadline but not yet stored as rejected.</param>
/// <param name="OutboxBacklog">Events not yet published.</param>
/// <param name="OutboxOldestAgeSeconds">Age of the oldest unpublished event; <c>0</c> when none.</param>
public sealed record ReconciliationReport(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DriftRow> Drift,
    long UnsettledHolds,
    long UnsettledWaiting,
    long OutboxBacklog,
    long OutboxOldestAgeSeconds)
{
    /// <summary>
    /// The number of drifting rows; must be 0.
    /// </summary>
    public int DriftRows => Drift.Count;
}

/// <summary>
/// One counter that disagrees with what it should equal.
/// </summary>
/// <param name="Check">Which comparison found it: <c>inventory_vs_ledger</c>, <c>quota_vs_ledger</c> or <c>inventory_vs_reservations</c>.</param>
/// <param name="Key">The row: pool and class, or customer, class and region.</param>
/// <param name="Counter"><c>held</c> or <c>used</c>.</param>
/// <param name="Expected">What the ledger or the reservations say.</param>
/// <param name="Actual">What the counter says.</param>
public sealed record DriftRow(string Check, string Key, string Counter, long Expected, long Actual);
