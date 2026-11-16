using Arvan.Capacity.Application.Repositories.Readonly;

namespace Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;

/// <summary>
/// The output of <see cref="SubmitReservationUseCase"/>. A refused request is an output, not an
/// exception: the rejection is stored, so it must commit and replay.
/// </summary>
/// <param name="Reservation">The stored reservation: held, waiting or rejected.</param>
/// <param name="Replayed">Whether this is the stored response of an earlier call with the same key.</param>
/// <param name="RetryAfterSeconds">For a rejection that retrying can fix, how long to wait.</param>
public sealed record SubmitReservationOutput(ReservationSummary Reservation, bool Replayed, int? RetryAfterSeconds = null);
