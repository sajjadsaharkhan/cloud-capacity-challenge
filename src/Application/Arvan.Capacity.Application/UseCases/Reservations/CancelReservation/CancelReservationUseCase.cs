using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Mappers.Reservations;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Reservations.CancelReservation;

/// <summary>
/// Cancels a held reservation: its held capacity returns at once. Corresponds to
/// <c>POST /v1/reservations/{id}/cancel</c>.
/// </summary>
public sealed class CancelReservationUseCase(
    ITransactionManager transactionManager,
    IClock clock,
    IIdempotencyOperations idempotency,
    IReservationOperations reservations,
    ICapacityOperations capacity)
    : IUseCase<CancelReservationInput, CancelReservationOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public Task<CancelReservationOutput> ExecuteAsync(CancelReservationInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            // ── validate ──
            var claim = await idempotency.ClaimAsync<CancelReservationOutput>(
                input.CustomerId, input.IdempotencyKey, $"cancel:{input.Id}", new { input.Id }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            // ── mutate ──
            var now = await clock.GetNowAsync(token);
            var reservation = await reservations.GetForUpdateAsync(input.Id, input.CustomerId, token);

            // A wait held nothing, so there is nothing to give back.
            var wasHeld = reservation.StoredStatus is ReservationStatus.Held;

            if (await reservations.CancelAsync(reservation, now, input.Actor, token) && wasHeld)
                await capacity.ReturnAsync([reservation], now, token);

            // ── shape ──
            var output = new CancelReservationOutput(reservation.ToSummary(now), Replayed: false);
            await idempotency.CompleteAsync(input.CustomerId, input.IdempotencyKey, reservation.Id, output, token);
            return output;
        }, ct);
}
