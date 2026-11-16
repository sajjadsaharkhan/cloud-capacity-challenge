using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// Chooses where a request is attempted. A pure function of a capacity snapshot: the snapshot only
/// ranks the candidates, and the conditional <c>UPDATE</c> that follows decides whether one holds.
/// </summary>
public static class HoldPlanner
{
    /// <summary>
    /// The most zones one request is attempted in.
    /// </summary>
    public const int MaxZones = 2;

    /// <summary>
    /// The most hosts one zone is attempted with.
    /// </summary>
    public const int MaxHosts = 3;

    /// <summary>
    /// The most attempts one request makes in total.
    /// </summary>
    public const int MaxAttempts = 4;

    /// <summary>
    /// Builds the plans to attempt, best first. Zones are ranked by the smallest ratio of free
    /// capacity to requested quantity across the zone- and host-scoped lines, highest first, so the
    /// request lands where it fits most comfortably. Within a zone, host-scoped lines take the hosts
    /// by best fit: the smallest free capacity that still covers the quantity.
    /// </summary>
    /// <param name="requestedZone">The zone the client named, or <c>null</c> to let the service pick.</param>
    /// <param name="lines">The lines requested.</param>
    /// <param name="snapshots">The inventories of the region for the requested classes.</param>
    /// <returns>At most <see cref="MaxAttempts"/> plans; empty when no zone has a pool for every class.</returns>
    public static IReadOnlyList<HoldPlan> Plan(
        string? requestedZone,
        IReadOnlyList<ReservationLine> lines,
        IReadOnlyCollection<InventorySnapshot> snapshots)
    {
        var available = snapshots.Where(s => s.AdminState is PoolAdminState.Available).ToList();
        var zoneLines = lines.Where(l => l.ResourceClass.ScopeOf() is ResourceScope.Zone).ToList();
        var hostLines = lines.Where(l => l.ResourceClass.ScopeOf() is ResourceScope.Host).ToList();
        var regionLines = lines.Where(l => l.ResourceClass.ScopeOf() is ResourceScope.Region).ToList();

        var regionAllocations = new List<LineAllocation>();
        foreach (var line in regionLines)
        {
            var pool = available.FirstOrDefault(s => s.Zone is null && s.ResourceClass == line.ResourceClass);
            if (pool is null)
                return [];

            regionAllocations.Add(new LineAllocation(pool.PoolId, line.ResourceClass, line.Quantity));
        }

        if (zoneLines.Count + hostLines.Count is 0)
            return [new HoldPlan(null, regionAllocations)];

        var zones = requestedZone is not null
            ? [requestedZone]
            : available.Where(s => s.Zone is not null).Select(s => s.Zone!).Distinct().ToList();

        var plans = new List<(HoldPlan Plan, double Score)>();
        foreach (var zone in zones)
        {
            var allocations = new List<LineAllocation>(regionAllocations);
            var score = double.MaxValue;
            var feasible = true;

            foreach (var line in zoneLines)
            {
                var pool = available.FirstOrDefault(s => s.Zone == zone && s.HostRef is null && s.ResourceClass == line.ResourceClass);
                if (pool is null)
                {
                    feasible = false;
                    break;
                }

                allocations.Add(new LineAllocation(pool.PoolId, line.ResourceClass, line.Quantity));
                score = Math.Min(score, (double)pool.Free / line.Quantity);
            }

            if (!feasible)
                continue;

            // One variant per candidate host for the first host-scoped line; further host lines
            // follow the same host. A request is served from a single host.
            var hostVariants = new List<List<LineAllocation>> { new() };
            if (hostLines.Count > 0)
            {
                hostVariants = [];
                var first = hostLines[0];
                var hosts = available
                    .Where(s => s.Zone == zone && s.HostRef is not null && s.ResourceClass == first.ResourceClass)
                    .ToList();

                var ordered = hosts.Where(h => h.Free >= first.Quantity).OrderBy(h => h.Free)
                    .Concat(hosts.Where(h => h.Free < first.Quantity).OrderByDescending(h => h.Free))
                    .Take(MaxHosts);

                foreach (var host in ordered)
                    hostVariants.Add([.. hostLines.Select(l => new LineAllocation(host.PoolId, l.ResourceClass, l.Quantity))]);

                if (hostVariants.Count is 0)
                    continue;

                score = Math.Min(score, hosts.Max(h => (double)h.Free) / first.Quantity);
            }

            foreach (var variant in hostVariants)
                plans.Add((new HoldPlan(zone, [.. allocations, .. variant]), score));
        }

        // At most two zones, best first, and at most four attempts across them.
        var bestZones = plans.GroupBy(p => p.Plan.Zone).OrderByDescending(g => g.Max(p => p.Score)).Take(MaxZones).Select(g => g.Key).ToList();

        return [.. plans
            .Where(p => bestZones.Contains(p.Plan.Zone))
            .OrderBy(p => bestZones.IndexOf(p.Plan.Zone))
            .Take(MaxAttempts)
            .Select(p => p.Plan)];
    }
}
