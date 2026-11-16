using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <inheritdoc cref="IProductRepository"/>
internal sealed class ProductRepository(PostgresSession session, IMemoryCache cache, IOptions<CapacityOptions> options)
    : IProductRepository
{
    internal const string Columns =
        "code AS id, status, resource_classes, hold_ttl_seconds, wait_max_seconds, max_active_holds, max_waiting, max_pool_share_pct";

    /// <inheritdoc cref="IProductRepository.FindAsync"/>
    public async Task<Product?> FindAsync(string code, CancellationToken ct)
    {
        // Cached for a couple of seconds: policy changes apply to new reservations only, and a hold
        // path that read the catalog on every submit would add a query to the hottest path.
        if (cache.TryGetValue(CacheKey(code), out Product? cached))
            return cached;

        var product = await session.QuerySingleOrDefaultAsync<Product>(new CommandDefinition(
            $"SELECT {Columns} FROM products WHERE code = @code",
            new { code },
            session.Transaction,
            cancellationToken: ct));

        if (product is not null)
            cache.Set(CacheKey(code), product, TimeSpan.FromSeconds(options.Value.CatalogCacheSeconds));

        return product;
    }

    /// <inheritdoc cref="IProductRepository.UpsertAsync"/>
    public async Task UpsertAsync(string code, ProductDefinition definition, CancellationToken ct)
    {
        await session.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO products (code, status, resource_classes, hold_ttl_seconds, wait_max_seconds,
                                  max_active_holds, max_waiting, max_pool_share_pct)
            VALUES (@code, @status, @classes, @HoldTtlSeconds, @WaitMaxSeconds, @MaxActiveHolds, @MaxWaiting, @MaxPoolSharePct)
            ON CONFLICT (code) DO UPDATE
            SET status = @status, resource_classes = @classes, hold_ttl_seconds = @HoldTtlSeconds,
                wait_max_seconds = @WaitMaxSeconds, max_active_holds = @MaxActiveHolds, max_waiting = @MaxWaiting,
                max_pool_share_pct = @MaxPoolSharePct
            """,
            new
            {
                code,
                status = DbEnum.ToDb(definition.Status),
                classes = definition.ResourceClasses.Select(c => DbEnum.ToDb(c)).ToArray(),
                definition.HoldTtlSeconds,
                definition.WaitMaxSeconds,
                definition.MaxActiveHolds,
                definition.MaxWaiting,
                definition.MaxPoolSharePct,
            },
            session.Transaction,
            cancellationToken: ct));

        cache.Remove(CacheKey(code));
    }

    private static string CacheKey(string code) => $"product:{code}";
}
