using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Catalog;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Application.Telemetry;
using Arvan.Capacity.Domain.Exceptions;

namespace Arvan.Capacity.Application.UseCases.Reservations.PromoteWaiting;

/// <summary>
/// Gives waiting requests the capacity that has come back, oldest first. Triggered by the sweeper.
/// A request whose capacity is still short stays waiting under its own savepoint; a smaller later
/// request that fits may be served first, and a large request's starvation is bounded by its
/// deadline.
/// </summary>
public sealed class PromoteWaitingUseCase(
    ITransactionManager transactionManager,
    IClock clock,
    IReservationOperations reservations,
    ICatalogOperations catalog,
    ICapacityOperations capacity)
    : IUseCase<PromoteWaitingInput, PromoteWaitingOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public Task<PromoteWaitingOutput> ExecuteAsync(PromoteWaitingInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            var now = await clock.GetNowAsync(token);
            var waiting = await reservations.LockWaitingAsync(input.BatchSize, token);
            var promoted = 0;

            foreach (var reservation in waiting)
            {
                var product = await catalog.GetAsync(reservation.ProductCode, token);

                IReadOnlyList<Domain.Entities.Capacity.HoldPlan> plans;
                try
                {
                    plans = (await capacity.PlanHoldAsync(reservation.Region, reservation.Zone, [.. reservation.Lines], token)).Plans;
                }
                catch (DomainValidationException)
                {
                    // The inventory rules changed since the request was accepted; it can only time out.
                    continue;
                }

                foreach (var plan in plans)
                {
                    var kept = await transactionManager.AttemptAsync(async attemptToken =>
                    {
                        var result = await capacity.TryHoldAsync(
                            reservation.Id, reservation.CustomerId, reservation.Region, plan, product, now, attemptToken);

                        if (result is not HoldResult.Held)
                            return false;

                        await reservations.PromoteAsync(reservation, plan, product, now, attemptToken);
                        return true;
                    }, token);

                    if (kept)
                    {
                        promoted++;
                        break;
                    }
                }
            }

            if (promoted > 0)
                CapacityTelemetry.Transitions.Add(promoted, new("event", "promote"), new("outcome", "ok"));

            return new PromoteWaitingOutput(waiting.Count, promoted);
        }, ct);
}
