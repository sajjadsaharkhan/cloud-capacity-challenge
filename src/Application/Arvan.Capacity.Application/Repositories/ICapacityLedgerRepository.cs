using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;


namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The only port allowed to change an inventory's <c>held</c> and <c>used</c> counters. Every
/// change appends a ledger row in the same transaction, so each counter is reproducible from the
/// ledger. All methods take locks in the global order — reservation rows, then inventory rows,
/// sorted by pool and class — which makes a deadlock cycle impossible.
/// </summary>
public interface ICapacityLedgerRepository
{
    /// <summary>
    /// Reads, without locks, the inventories of every pool in a region for the given classes.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="classes">The resource classes wanted.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<InventorySnapshot>> GetSnapshotsAsync(
        string region,
        IReadOnlyCollection<ResourceClass> classes,
        CancellationToken ct);

    /// <summary>
    /// Takes every line out of free capacity, or none of them. For each line, in class order, a
    /// conditional <c>UPDATE</c> charges the customer's quota while <c>held + used + q</c> stays
    /// within its limit; then, for a capped product, the customer's share of the pool is checked
    /// under the quota row's lock; then a conditional <c>UPDATE</c> takes the units from every
    /// inventory in pool-and-class order while <c>held + used + q</c> fits the sellable capacity of
    /// an available pool. Appends the <c>HOLD</c> ledger rows when all succeed.
    /// </summary>
    /// <param name="request">The reservation, its customer and the pools to take from.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <returns><see cref="HoldResult.Held"/>, or why not; on anything else nothing is kept in effect, so the caller's savepoint can roll back.</returns>
    Task<HoldResult> TryHoldAsync(LedgerHold request, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// The earliest lease end among the holds on these pools, which is when capacity next returns.
    /// </summary>
    /// <param name="poolIds">The pools to look in.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<DateTimeOffset?> GetEarliestHoldExpiryAsync(IReadOnlyCollection<int> poolIds, CancellationToken ct);

    /// <summary>
    /// Applies an operator's correction to a counter, as one <c>ADJUST</c> ledger row.
    /// </summary>
    /// <param name="adjustment">The correction.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the target counter does not exist.</exception>
    Task AdjustAsync(LedgerAdjustment adjustment, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Moves a reservation's quantities from held to used, in quota and inventory, and appends the <c>COMMIT</c> rows.
    /// </summary>
    /// <param name="reservation">The confirmed reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task CommitAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Takes reservations' quantities out of held, in quota and inventory, and appends the <c>RETURN</c> rows.
    /// </summary>
    /// <param name="reservations">The cancelled or expired reservations; counters are summed per pool and class across the batch.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task ReturnAsync(IReadOnlyCollection<Reservation> reservations, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Takes a reservation's quantities out of used, in quota and inventory, and appends the <c>RELEASE</c> rows.
    /// </summary>
    /// <param name="reservation">The released reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task ReleaseAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct);
}

/// <summary>
/// An operator's correction to a counter. Either an inventory (<paramref name="PoolId"/>) or a
/// customer's quota (<paramref name="CustomerId"/> and <paramref name="Region"/>), never both.
/// </summary>
/// <param name="PoolId">The pool, for an inventory correction.</param>
/// <param name="CustomerId">The customer, for a quota correction.</param>
/// <param name="Region">The region, for a quota correction.</param>
/// <param name="ResourceClass">The class corrected.</param>
/// <param name="DeltaHeld">The change to <c>held</c>.</param>
/// <param name="DeltaUsed">The change to <c>used</c>.</param>
/// <param name="ActorId">The operator.</param>
/// <param name="Reason">Why; required.</param>
public sealed record LedgerAdjustment(
    int? PoolId,
    string? CustomerId,
    string? Region,
    ResourceClass ResourceClass,
    long DeltaHeld,
    long DeltaUsed,
    string ActorId,
    string Reason);
