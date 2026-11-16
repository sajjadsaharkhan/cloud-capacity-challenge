namespace Arvan.Capacity.Application.UseCases.Events.RelayEvents;

/// <summary>
/// The input to <see cref="RelayEventsUseCase"/>.
/// </summary>
/// <param name="BatchSize">The largest number of events to relay in one transaction.</param>
public sealed record RelayEventsInput(int BatchSize);
