namespace Arvan.Capacity.Application.UseCases.Reservations.TimeOutWaiting;

/// <summary>
/// The input to <see cref="TimeOutWaitingUseCase"/>.
/// </summary>
/// <param name="BatchSize">The largest number of waits to finalize in one transaction.</param>
public sealed record TimeOutWaitingInput(int BatchSize);
