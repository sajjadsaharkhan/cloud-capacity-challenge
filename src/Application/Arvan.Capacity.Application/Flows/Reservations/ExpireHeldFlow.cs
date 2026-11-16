using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Application.Telemetry;

namespace Arvan.Capacity.Application.Flows.Reservations;

/// <summary>
/// Stores the end of every hold whose lease ran out and gives its capacity back: the reservation
/// transition and the counter movement, together. Shared by the sweeper and by the hold path's
/// inline finalizer, which is what makes it a Flow.
/// </summary>
/// <remarks>
/// Runs inside the transaction its caller opened. Locks reservation rows first, then inventory
/// rows, the global order, and skips rows another sweeper already holds.
/// </remarks>
public sealed class ExpireHeldFlow(
    IClock clock,
    IReservationOperations reservations,
    ICapacityOperations capacity)
    : IFlow<ExpireHeldFlowInput, int>
{
    /// <inheritdoc cref="IFlow{TInput,TOutput}.ExecuteAsync"/>
    /// <returns>The number of holds finalized.</returns>
    public async Task<int> ExecuteAsync(ExpireHeldFlowInput input, CancellationToken ct)
    {
        var now = await clock.GetNowAsync(ct);
        var expired = await reservations.LockExpiredHeldAsync(input.BatchSize, input.PoolIds, ct);

        foreach (var reservation in expired)
        {
            CapacityTelemetry.ExpiryLag.Record(
                Math.Max(0, (now - reservation.ExpiresAt!.Value).TotalSeconds), new KeyValuePair<string, object?>("region", reservation.Region));
            await reservations.ExpireAsync(reservation, now, ct);
        }

        if (expired.Count > 0)
            CapacityTelemetry.Transitions.Add(expired.Count, new("event", "expire"), new("outcome", "ok"));

        await capacity.ReturnAsync(expired, now, ct);
        return expired.Count;
    }
}
