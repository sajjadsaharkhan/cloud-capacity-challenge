namespace Arvan.Capacity.Application.UseCases.Reservations.TimeOutWaiting;

/// <summary>
/// The output of <see cref="TimeOutWaitingUseCase"/>.
/// </summary>
/// <param name="TimedOut">The number of waits finalized. A full batch means more may be waiting.</param>
public sealed record TimeOutWaitingOutput(int TimedOut);
