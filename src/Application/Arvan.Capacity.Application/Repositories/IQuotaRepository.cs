using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The write-side port for customer quota limits. The counters are moved only by the capacity
/// ledger.
/// </summary>
public interface IQuotaRepository
{
    /// <summary>
    /// Sets a customer's contract limit for a class in a region, creating the row if needed.
    /// Lowering it below current usage is allowed: existing holds stay, new ones fail.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="resourceClass">The class.</param>
    /// <param name="region">The region.</param>
    /// <param name="limit">The new limit.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task SetLimitAsync(string customerId, ResourceClass resourceClass, string region, long limit, CancellationToken ct);
}
