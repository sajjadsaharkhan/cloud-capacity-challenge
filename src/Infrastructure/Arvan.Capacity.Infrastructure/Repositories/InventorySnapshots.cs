using System.Data;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Infrastructure.Persistence;
using Dapper;

namespace Arvan.Capacity.Infrastructure.Repositories;

/// <summary>
/// The one query that reads a region's inventories without locks, shared by the write side (inside
/// the open transaction) and the read side (on its own connection).
/// </summary>
internal static class InventorySnapshots
{
    private sealed record Row(
        int PoolId, string? Zone, string? HostRef, string AdminState, string ResourceClass, long Total, long Reserved,
        decimal AllocationRatio, long Held, long Used, long MinUnit, long StepSize, long MaxUnit);

    /// <summary>
    /// Reads the inventories of every pool in a region for the given classes.
    /// </summary>
    public static async Task<IReadOnlyList<InventorySnapshot>> QueryAsync(
        IDbConnection connection, IDbTransaction? transaction, string region, IReadOnlyCollection<ResourceClass> classes, CancellationToken ct)
    {
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT p.id AS PoolId, p.zone AS Zone, p.host_ref AS HostRef, p.admin_state AS AdminState,
                   i.resource_class AS ResourceClass, i.total AS Total, i.reserved AS Reserved,
                   i.allocation_ratio AS AllocationRatio, i.held AS Held, i.used AS Used,
                   i.min_unit AS MinUnit, i.step_size AS StepSize, i.max_unit AS MaxUnit
            FROM pools p JOIN inventories i ON i.pool_id = p.id
            WHERE p.region = @region AND p.admin_state <> 'RETIRED' AND i.resource_class = ANY(@classes)
            ORDER BY p.id, i.resource_class
            """,
            new { region, classes = classes.Select(c => DbEnum.ToDb(c)).ToArray() },
            transaction,
            cancellationToken: ct));

        return
        [
            .. rows.Select(r => new InventorySnapshot(
                r.PoolId, r.Zone, r.HostRef, DbEnum.FromDb<PoolAdminState>(r.AdminState), DbEnum.FromDb<ResourceClass>(r.ResourceClass),
                r.Total, r.Reserved, r.AllocationRatio, r.Held, r.Used, r.MinUnit, r.StepSize, r.MaxUnit)),
        ];
    }
}
