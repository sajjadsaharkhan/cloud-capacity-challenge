using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Arvan.Capacity.Infrastructure.Repositories.Readonly;

/// <inheritdoc cref="ICapacityReadonlyRepository"/>
internal sealed class CapacityReadonlyRepository(NpgsqlDataSource dataSource, IOptions<CapacityOptions> options)
    : ICapacityReadonlyRepository
{
    private sealed class Row
    {
        public string Region { get; set; } = default!;
        public int PoolId { get; set; }
        public string? Zone { get; set; }
        public string? HostRef { get; set; }
        public string ResourceClass { get; set; } = default!;
        public long Sellable { get; set; }
        public long Held { get; set; }
        public long Used { get; set; }
        public long ExpiredHeld { get; set; }
    }

    /// <inheritdoc cref="ICapacityReadonlyRepository.GetInventoryRowsAsync"/>
    public async Task<IReadOnlyList<CapacityInventoryRow>> GetInventoryRowsAsync(string? region, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT p.region AS Region, p.id AS PoolId, p.zone AS Zone, p.host_ref AS HostRef, i.resource_class AS ResourceClass,
                   floor((i.total - i.reserved) * i.allocation_ratio)::bigint AS Sellable, i.held AS Held, i.used AS Used,
                   COALESCE((SELECT sum(l.quantity) FROM reservation_lines l JOIN reservations r ON r.id = l.reservation_id
                             WHERE l.pool_id = i.pool_id AND l.resource_class = i.resource_class
                               AND r.stored_status = 'HELD' AND r.expires_at <= now()), 0)::bigint AS ExpiredHeld
            FROM pools p JOIN inventories i ON i.pool_id = p.id
            WHERE p.admin_state = 'AVAILABLE' AND (@region::text IS NULL OR p.region = @region)
            ORDER BY p.region, p.id, i.resource_class
            """,
            new { region },
            cancellationToken: ct));

        return
        [
            .. rows.Select(r => new CapacityInventoryRow(
                r.Region, r.PoolId, r.Zone, r.HostRef, DbEnum.FromDb<ResourceClass>(r.ResourceClass),
                r.Sellable, r.Held, r.Used, r.ExpiredHeld)),
        ];
    }

    /// <inheritdoc cref="ICapacityReadonlyRepository.GetProductsAsync"/>
    public async Task<IReadOnlyList<Product>> GetProductsAsync(string? productCode, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        return (await connection.QueryAsync<Product>(new CommandDefinition(
            $"SELECT {ProductRepository.Columns} FROM products WHERE @productCode::text IS NULL OR code = @productCode ORDER BY code",
            new { productCode },
            cancellationToken: ct))).ToList();
    }

    /// <inheritdoc cref="ICapacityReadonlyRepository.GetSnapshotsAsync"/>
    public async Task<IReadOnlyList<InventorySnapshot>> GetSnapshotsAsync(
        string region, IReadOnlyCollection<ResourceClass> classes, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await InventorySnapshots.QueryAsync(connection, null, region, classes, ct);
    }

    /// <inheritdoc cref="ICapacityReadonlyRepository.GetQuotaRoomAsync"/>
    public async Task<IReadOnlyDictionary<ResourceClass, long>> GetQuotaRoomAsync(
        string customerId, string region, IReadOnlyCollection<ResourceClass> classes, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = (await connection.QueryAsync<(string ResourceClass, long Room)>(new CommandDefinition(
            """
            SELECT resource_class AS ResourceClass, quota_limit - held - used AS Room
            FROM quotas WHERE customer_id = @customerId AND region = @region AND resource_class = ANY(@classes)
            """,
            new { customerId, region, classes = classes.Select(c => DbEnum.ToDb(c)).ToArray() },
            cancellationToken: ct))).ToDictionary(r => DbEnum.FromDb<ResourceClass>(r.ResourceClass), r => r.Room);

        // No row means the class's default quota, never unlimited.
        return classes.ToDictionary(c => c, c => rows.TryGetValue(c, out var room) ? room : options.Value.DefaultQuotaOf(c));
    }
}
