using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <inheritdoc cref="IQuotaRepository"/>
internal sealed class QuotaRepository(PostgresSession session) : IQuotaRepository
{
    /// <inheritdoc cref="IQuotaRepository.SetLimitAsync"/>
    public Task SetLimitAsync(string customerId, ResourceClass resourceClass, string region, long limit, CancellationToken ct) =>
        session.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO quotas (customer_id, resource_class, region, quota_limit)
            VALUES (@customerId, @resourceClass, @region, @limit)
            ON CONFLICT (customer_id, resource_class, region) DO UPDATE SET quota_limit = @limit
            """,
            new { customerId, resourceClass = DbEnum.ToDb(resourceClass), region, limit },
            session.Transaction,
            cancellationToken: ct));
}
