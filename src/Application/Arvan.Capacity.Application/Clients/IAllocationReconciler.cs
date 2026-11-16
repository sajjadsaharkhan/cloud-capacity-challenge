namespace Arvan.Capacity.Application.Clients;

/// <summary>
/// Finds allocations whose service no longer exists, by asking the product services what they
/// actually run. Only the interface ships in this version: it needs the real inventory of those
/// services. A finding is released through the release endpoint with reason <c>Reconciled</c>.
/// </summary>
public interface IAllocationReconciler
{
    /// <summary>
    /// Lists confirmed, still-allocated reservations that no product service claims.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<Guid>> FindOrphanedAllocationsAsync(CancellationToken ct);
}
