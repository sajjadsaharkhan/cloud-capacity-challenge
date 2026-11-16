using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Mappers.Reservations;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Idempotency;
using Arvan.Capacity.Application.Operations.Reservations;

namespace Arvan.Capacity.Application.UseCases.Reservations.ConfirmReservation;

/// <summary>
/// Confirms a held reservation: its held capacity becomes allocated. Corresponds to
/// <c>POST /v1/reservations/{id}/confirm</c>.
/// </summary>
public sealed class ConfirmReservationUseCase(
    ITransactionManager transactionManager,
    IClock clock,
    IIdempotencyOperations idempotency,
    IReservationOperations reservations,
    ICapacityOperations capacity)
    : IUseCase<ConfirmReservationInput, ConfirmReservationOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public Task<ConfirmReservationOutput> ExecuteAsync(ConfirmReservationInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            // ── validate ──
            var claim = await idempotency.ClaimAsync<ConfirmReservationOutput>(
                input.CustomerId, input.IdempotencyKey, $"confirm:{input.Id}", new { input.Id }, token);

            if (claim.IsReplay)
                return claim.Response! with { Replayed = true };

            // ── mutate ──
            var now = await clock.GetNowAsync(token);
            var reservation = await reservations.GetForUpdateAsync(input.Id, input.CustomerId, token);

            if (await reservations.ConfirmAsync(reservation, now, input.Actor, token))
                await capacity.CommitAsync(reservation, now, token);

            // ── shape ──
            var output = new ConfirmReservationOutput(reservation.ToSummary(now), Replayed: false);
            await idempotency.CompleteAsync(input.CustomerId, input.IdempotencyKey, reservation.Id, output, token);
            return output;
        }, ct);
}
