namespace Arvan.Capacity.Application.UseCases.Admin.CancelOpenReservations;

/// <summary>
/// The output of <see cref="CancelOpenReservationsUseCase"/>.
/// </summary>
/// <param name="Cancelled">How many holds and waiting requests were cancelled.</param>
public sealed record CancelOpenReservationsOutput(int Cancelled);
