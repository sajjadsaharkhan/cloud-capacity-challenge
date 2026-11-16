namespace Arvan.Capacity.Application.Flows.Reservations;

/// <summary>
/// The input to <see cref="ExpireHeldFlow"/>.
/// </summary>
/// <param name="BatchSize">The largest number of holds to finalize in one run.</param>
/// <param name="PoolIds">When set, only holds with a line in one of these pools; <c>null</c> for the sweeper's whole backlog.</param>
public sealed record ExpireHeldFlowInput(int BatchSize, IReadOnlyCollection<int>? PoolIds);
