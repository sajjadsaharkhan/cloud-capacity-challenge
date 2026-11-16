using Arvan.Capacity.Application.Operations.Quotas;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Capacity;

namespace Arvan.Capacity.Application.Operations.Implementations.Quotas;

/// <inheritdoc cref="IQuotaOperations"/>
public sealed class QuotaOperations(IQuotaRepository quotas) : IQuotaOperations
{
    /// <inheritdoc cref="IQuotaOperations.SetLimitAsync"/>
    public Task SetLimitAsync(string customerId, ResourceClass resourceClass, string region, long limit, CancellationToken ct) =>
        limit < 0
            ? throw new InventoryConfigurationInvalidException()
            : quotas.SetLimitAsync(customerId, resourceClass, region, limit, ct);
}
