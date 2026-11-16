using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <inheritdoc cref="IPoolRepository"/>
internal sealed class PoolRepository(PostgresSession session) : IPoolRepository
{
    private sealed record InventoryRow(
        int PoolId, string? Zone, string? HostRef, string AdminState, string ResourceClass, long Total, long Reserved,
        decimal AllocationRatio, long Held, long Used, long MinUnit, long StepSize, long MaxUnit);

    private sealed class PoolRow
    {
        public int Id { get; set; }
        public string Region { get; set; } = default!;
        public string? Zone { get; set; }
        public string? HostRef { get; set; }
        public string AdminState { get; set; } = default!;
    }

    /// <inheritdoc cref="IRepository{TAggregate}.AddAsync"/>
    public async Task AddAsync(Pool pool, CancellationToken ct)
    {
        await session.ExecuteAsync(new CommandDefinition(
            "INSERT INTO pools (id, region, zone, host_ref, admin_state) VALUES (@Id, @Region, @Zone, @HostRef, @State)",
            new { pool.Id, pool.Region, pool.Zone, pool.HostRef, State = DbEnum.ToDb(pool.AdminState) },
            session.Transaction,
            cancellationToken: ct));

        // The id was chosen by the operator, so move the sequence past it for pools created without one.
        await session.ExecuteAsync(new CommandDefinition(
            "SELECT setval(pg_get_serial_sequence('pools', 'id'), GREATEST((SELECT max(id) FROM pools), 1))",
            transaction: session.Transaction,
            cancellationToken: ct));
    }

    /// <inheritdoc cref="IPoolRepository.FindForUpdateAsync"/>
    public async Task<Pool?> FindForUpdateAsync(int id, CancellationToken ct)
    {
        var row = await session.QuerySingleOrDefaultAsync<PoolRow>(new CommandDefinition(
            "SELECT id AS Id, region AS Region, zone AS Zone, host_ref AS HostRef, admin_state AS AdminState FROM pools WHERE id = @id FOR UPDATE",
            new { id },
            session.Transaction,
            cancellationToken: ct));

        if (row is null)
            return null;

        // Rebuilt through the factory and the state machine's own moves, so the entity never holds a state it could not reach.
        var pool = Pool.Register(row.Id, row.Region, row.Zone, row.HostRef);
        switch (DbEnum.FromDb<PoolAdminState>(row.AdminState))
        {
            case PoolAdminState.Available: pool.Activate(); break;
            case PoolAdminState.Offline: pool.Activate(); pool.TakeOffline(); break;
            case PoolAdminState.Retired: pool.Retire(hasHeldOrUsedUnits: false); break;
        }

        return pool;
    }

    /// <inheritdoc cref="IPoolRepository.UpdateAsync"/>
    public Task UpdateAsync(Pool pool, CancellationToken ct) =>
        session.ExecuteAsync(new CommandDefinition(
            "UPDATE pools SET region = @Region, zone = @Zone, host_ref = @HostRef, admin_state = @State, updated_at = now() WHERE id = @Id",
            new { pool.Id, pool.Region, pool.Zone, pool.HostRef, State = DbEnum.ToDb(pool.AdminState) },
            session.Transaction,
            cancellationToken: ct));

    /// <inheritdoc cref="IPoolRepository.HasHeldOrUsedUnitsAsync"/>
    public Task<bool> HasHeldOrUsedUnitsAsync(int poolId, CancellationToken ct) =>
        session.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM inventories WHERE pool_id = @poolId AND (held > 0 OR used > 0))",
            new { poolId },
            session.Transaction,
            cancellationToken: ct));

    /// <inheritdoc cref="IPoolRepository.FindInventoryForUpdateAsync"/>
    public async Task<InventorySnapshot?> FindInventoryForUpdateAsync(int poolId, ResourceClass resourceClass, CancellationToken ct)
    {
        var row = await session.QuerySingleOrDefaultAsync<InventoryRow>(new CommandDefinition(
            """
            SELECT i.pool_id AS PoolId, p.zone AS Zone, p.host_ref AS HostRef, p.admin_state AS AdminState,
                   i.resource_class AS ResourceClass, i.total AS Total, i.reserved AS Reserved,
                   i.allocation_ratio AS AllocationRatio, i.held AS Held, i.used AS Used,
                   i.min_unit AS MinUnit, i.step_size AS StepSize, i.max_unit AS MaxUnit
            FROM inventories i JOIN pools p ON p.id = i.pool_id
            WHERE i.pool_id = @poolId AND i.resource_class = @resourceClass
            FOR UPDATE OF i
            """,
            new { poolId, resourceClass = DbEnum.ToDb(resourceClass) },
            session.Transaction,
            cancellationToken: ct));

        return row is null
            ? null
            : new InventorySnapshot(
                row.PoolId, row.Zone, row.HostRef, DbEnum.FromDb<PoolAdminState>(row.AdminState), resourceClass,
                row.Total, row.Reserved, row.AllocationRatio, row.Held, row.Used, row.MinUnit, row.StepSize, row.MaxUnit);
    }

    /// <inheritdoc cref="IPoolRepository.UpsertInventoryAsync"/>
    public Task UpsertInventoryAsync(int poolId, ResourceClass resourceClass, InventoryConfiguration configuration, CancellationToken ct) =>
        session.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO inventories (pool_id, resource_class, total, reserved, allocation_ratio, min_unit, step_size, max_unit)
            VALUES (@poolId, @resourceClass, @Total, @Reserved, @AllocationRatio, @MinUnit, @StepSize, @MaxUnit)
            ON CONFLICT (pool_id, resource_class) DO UPDATE
            SET total = @Total, reserved = @Reserved, allocation_ratio = @AllocationRatio,
                min_unit = @MinUnit, step_size = @StepSize, max_unit = @MaxUnit
            """,
            new
            {
                poolId, resourceClass = DbEnum.ToDb(resourceClass), configuration.Total, configuration.Reserved,
                configuration.AllocationRatio, configuration.MinUnit, configuration.StepSize, configuration.MaxUnit,
            },
            session.Transaction,
            cancellationToken: ct));
}
