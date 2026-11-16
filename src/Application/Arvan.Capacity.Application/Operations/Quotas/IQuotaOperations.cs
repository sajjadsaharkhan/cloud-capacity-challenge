using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Operations.Quotas;

/// <summary>
/// Owns customer quota limits. Never begins or commits a transaction, never calls a peer Operation.
/// </summary>
public interface IQuotaOperations
{
    /// <summary>
    /// Sets a customer's contract limit for a class in a region.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="resourceClass">The class.</param>
    /// <param name="region">The region.</param>
    /// <param name="limit">The new limit, zero or more.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="Domain.Exceptions.DomainValidationException">Thrown when the limit is negative.</exception>
    Task SetLimitAsync(string customerId, ResourceClass resourceClass, string region, long limit, CancellationToken ct);
}
