using System.Diagnostics;
using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Flows.Reservations;
using Arvan.Capacity.Application.Mappers.Reservations;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Catalog;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Application.Telemetry;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;

/// <summary>
/// Reserves capacity across several resource lines, all or nothing. Corresponds to
/// <c>POST /v1/reservations</c>.
/// </summary>
/// <remarks>
/// One transaction, with one exception. When the capacity is short only because holds past their
/// lease are still stored as held, finalizing them inside this transaction would lock reservation
/// rows after inventory rows and break the global lock order. So this transaction is rolled back,
/// one short finalizing transaction runs in the normal order, and the submit is tried once more.
/// </remarks>
public sealed class SubmitReservationUseCase(
    ITransactionManager transactionManager,
    IClock clock,
    IIdempotencyOperations idempotency,
    ICatalogOperations catalog,
    IReservationOperations reservations,
    ICapacityOperations capacity,
    ExpireHeldFlow expireHeld)
    : IUseCase<SubmitReservationInput, SubmitReservationOutput>
{
    private const int FinalizeBatchSize = 200;

    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public async Task<SubmitReservationOutput> ExecuteAsync(SubmitReservationInput input, CancellationToken ct)
    {
        try
        {
            return await SubmitAsync(input, mayFinalizeExpiredHolds: true, ct);
        }
        catch (FinalizeExpiredHoldsFirstException finalize)
        {
            await transactionManager.RunAsync(
                token => expireHeld.ExecuteAsync(new ExpireHeldFlowInput(FinalizeBatchSize, finalize.PoolIds), token),
                ct);

            return await SubmitAsync(input, mayFinalizeExpiredHolds: false, ct);
        }
    }

    private Task<SubmitReservationOutput> SubmitAsync(
        SubmitReservationInput input, bool mayFinalizeExpiredHolds, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            var started = Stopwatch.GetTimestamp();
            using var activity = CapacityTelemetry.Source.StartActivity("reservation.submit");
            activity?.SetTag("customer.id", input.CustomerId);
            activity?.SetTag("region", input.Region);

            // ── validate ──
            var requested = input.Lines
                .OrderBy(l => l.ResourceClass)
                .Select(l => new ReservationLine(l.ResourceClass, l.Quantity))
                .ToList();

            IdempotencyClaim<SubmitReservationOutput> claim;
            using (CapacityTelemetry.Phase("claim"))
                claim = await idempotency.ClaimAsync<SubmitReservationOutput>(
                    input.CustomerId,
                    input.IdempotencyKey,
                    "submit",
                    new
                    {
                        input.Product,
                        input.Region,
                        input.Zone,
                        Lines = requested.Select(l => new { l.ResourceClass, l.Quantity }),
                        input.OnShortage,
                        input.ClientReference,
                    },
                    token);

            if (claim.IsReplay)
            {
                CapacityTelemetry.IdempotentReplays.Add(1, new KeyValuePair<string, object?>("operation", "submit"));
                return claim.Response! with { Replayed = true };
            }

            DateTimeOffset now;
            Domain.Entities.Products.Product product;
            CapacityPlanning planning;

            using (CapacityTelemetry.Phase("context"))
            {
                now = await clock.GetNowAsync(token);
                product = await catalog.GetReservableAsync(input.Product, requested.Select(l => l.ResourceClass), token);
                await reservations.EnsureClientReferenceAvailableAsync(input.CustomerId, input.ClientReference, token);
                await reservations.EnsureWithinLimitsAsync(input.CustomerId, product, input.OnShortage, now, token);
            }

            using (CapacityTelemetry.Phase("plan"))
                planning = await capacity.PlanHoldAsync(input.Region, input.Zone, requested, token);

            // ── mutate ──
            Reservation? reservation = null;
            SubmitReservationOutput? output = null;
            var refusal = HoldResult.CapacityShort;
            var sawPoolShare = false;

            using (CapacityTelemetry.Phase("write"))
            {
                // Hopeless: every plan already shows too little free. Do not take locks to be refused.
                var plans = planning.IsHopeless ? [] : planning.Plans;

                foreach (var plan in plans)
                {
                    // Found sold out a moment ago: skip the hot row and go straight to classification.
                    if (capacity.IsKnownSoldOut(plan))
                        continue;

                    var kept = await transactionManager.AttemptAsync(async attemptToken =>
                    {
                        // Queued, not sent: the request and its idempotency response travel in the same
                        // batch as the quota and ledger writes, so the contended inventory row can be the
                        // last thing locked before the commit.
                        var candidate = await reservations.HoldAsync(
                            input.CustomerId, product, input.Region, input.OnShortage, input.ClientReference,
                            plan, input.Actor, now, attemptToken);

                        var granted = new SubmitReservationOutput(candidate.ToSummary(now), Replayed: false);
                        await idempotency.CompleteAsync(input.CustomerId, input.IdempotencyKey, candidate.Id, granted, attemptToken);

                        var result = await capacity.TryHoldAsync(
                            candidate.Id, input.CustomerId, input.Region, plan, product, now, attemptToken);

                        if (result is not HoldResult.Held)
                        {
                            refusal = result;
                            return false;
                        }

                        reservation = candidate;
                        output = granted;
                        return true;
                    }, token);

                    if (kept)
                        break;

                    // The contract does not depend on which zone or host was tried; the share cap is per
                    // pool, so another host may still fit.
                    if (refusal is HoldResult.QuotaExceeded)
                        break;

                    sawPoolShare |= refusal is HoldResult.PoolShareExceeded;
                }
            }

            int? retryAfter = null;

            if (reservation is null)
            {
                using (CapacityTelemetry.Phase("refuse"))
                {
                    if (refusal is HoldResult.QuotaExceeded || sawPoolShare)
                    {
                        reservation = await reservations.RejectAsync(
                            input.CustomerId, input.Product, input.Region, input.OnShortage, input.ClientReference, requested,
                            refusal is HoldResult.QuotaExceeded ? ReasonCode.QuotaExceeded : ReasonCode.PoolShareExceeded,
                            input.Actor, now, token);
                    }
                    else
                    {
                        var shortage = await capacity.ClassifyShortageAsync(planning.Snapshots, input.Zone, requested, now, token);

                        if (mayFinalizeExpiredHolds && shortage.Reason is ReasonCode.CapacityTemporarilyUnavailable)
                        {
                            var poolIds = planning.Plans.SelectMany(p => p.Lines.Select(l => l.PoolId)).Distinct().ToList();
                            if (poolIds.Count > 0 && await reservations.ExpiredHeldExistsAsync(poolIds, token))
                                throw new FinalizeExpiredHoldsFirstException(poolIds);
                        }

                        if (shortage.Reason is ReasonCode.CapacityTemporarilyUnavailable && input.OnShortage is ShortagePolicy.Wait)
                        {
                            reservation = await reservations.WaitAsync(
                                input.CustomerId, product, input.Region, input.Zone, input.ClientReference, requested, input.Actor, now, token);
                        }
                        else
                        {
                            retryAfter = shortage.RetryAfterSeconds;
                            reservation = await reservations.RejectAsync(
                                input.CustomerId, input.Product, input.Region, input.OnShortage, input.ClientReference, requested,
                                shortage.Reason, input.Actor, now, token);
                        }
                    }
                }
            }

            // ── shape ──
            if (output is null)
            {
                output = new SubmitReservationOutput(reservation.ToSummary(now), Replayed: false, retryAfter);
                await idempotency.CompleteAsync(input.CustomerId, input.IdempotencyKey, reservation.Id, output, token);
            }

            var outcome = reservation.StoredStatus switch
            {
                ReservationStatus.Held => "held",
                ReservationStatus.Waiting => "waiting",
                _ => "rejected",
            };
            CapacityTelemetry.Holds.Add(
                1,
                new("region", input.Region), new("product", input.Product), new("outcome", outcome),
                new("code", reservation.ReasonCode?.ToString() ?? "none"));
            CapacityTelemetry.HoldDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalSeconds, new("region", input.Region), new("product", input.Product));
            activity?.SetTag("reservation.id", reservation.Id);
            activity?.SetTag("outcome", outcome);
            return output;
        }, ct);

    /// <summary>
    /// Thrown inside the submit transaction to roll it back and ask <see cref="ExecuteAsync"/> to
    /// finalize expired holds on these pools before trying once more.
    /// </summary>
    private sealed class FinalizeExpiredHoldsFirstException(IReadOnlyCollection<int> poolIds) : Exception
    {
        public IReadOnlyCollection<int> PoolIds { get; } = poolIds;
    }
}
