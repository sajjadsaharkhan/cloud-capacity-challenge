namespace Arvan.Capacity.Domain.Entities.Capacity;

/// <summary>
/// One way to serve a request: the zone to hold in and the pool each line would be drawn from.
/// </summary>
/// <param name="Zone">The zone of the zone-scoped lines, or <c>null</c> when every line is region-scoped.</param>
/// <param name="Lines">One allocation per requested line.</param>
public sealed record HoldPlan(string? Zone, IReadOnlyList<LineAllocation> Lines);
