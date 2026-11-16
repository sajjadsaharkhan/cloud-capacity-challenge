using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Domain.Entities.Reservations;

namespace Arvan.Capacity.Application.UseCases.Reservations.GetReservation;

/// <summary>
/// Retrieves a reservation as it reads right now. Corresponds to <c>GET /v1/reservations/{id}</c>.
/// A read: one readonly repository, no transaction.
/// </summary>
public sealed class GetReservationUseCase(IReservationReadonlyRepository reservations)
    : IUseCase<GetReservationInput, ReservationSummary>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="NotFoundException">Thrown when no such reservation exists for the customer.</exception>
    public async Task<ReservationSummary> ExecuteAsync(GetReservationInput input, CancellationToken ct) =>
        await reservations.GetByIdAsync(input.Id, input.CustomerId, ct)
        ?? throw new EntityNotFoundException<Reservation>(input.Id);
}
