using Arvan.Capacity.Application.Repositories.Readonly;

namespace Arvan.Capacity.Application.UseCases.Reservations.ConfirmReservation;

/// <summary>
/// The output of <see cref="ConfirmReservationUseCase"/>.
/// </summary>
/// <param name="Reservation">The reservation after the call.</param>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
public sealed record ConfirmReservationOutput(ReservationSummary Reservation, bool Replayed);
