using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Operations.Capacity;
using Arvan.Capacity.Application.Operations.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.CancelOpenReservations;

/// <summary>
/// Cleans up after a suspended customer: cancels their holds and waiting requests, 200 per
/// transaction. Confirmed allocations are untouched; their fate belongs to billing. Naturally
/// idempotent, so it takes no key. Corresponds to <c>POST /v1/admin/customers/{id}/cancel-open</c>.
/// </summary>
public sealed class CancelOpenReservationsUseCase(
    ITransactionManager transactionManager,
    IClock clock,
    IReservationOperations reservations,
    ICapacityOperations capacity)
    : IUseCase<CancelOpenReservationsInput, CancelOpenReservationsOutput>
{
    private const int BatchSize = 200;

    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when the caller is not an admin.</exception>
    public async Task<CancelOpenReservationsOutput> ExecuteAsync(CancelOpenReservationsInput input, CancellationToken ct)
    {
        if (input.Actor.Kind is not ActorKind.Admin)
            throw new ActorNotAllowedException("cancel a customer's open reservations");

        var total = 0;
        int batch;
        do
        {
            batch = await transactionManager.RunAsync(async token =>
            {
                var now = await clock.GetNowAsync(token);
                var open = await reservations.LockOpenAsync(input.CustomerId, BatchSize, token);
                var held = open.Where(r => r.StoredStatus is ReservationStatus.Held).ToList();

                foreach (var reservation in open)
                    await reservations.CancelAsync(reservation, now, input.Actor, token);

                await capacity.ReturnAsync(held, now, token);
                return open.Count;
            }, ct);

            total += batch;
        }
        while (batch == BatchSize);

        return new CancelOpenReservationsOutput(total);
    }
}
