namespace Arvan.Capacity.Application.UseCases.Events.RelayEvents;

/// <summary>
/// The output of <see cref="RelayEventsUseCase"/>.
/// </summary>
/// <param name="Relayed">The number of events published. A full batch means more may be waiting.</param>
public sealed record RelayEventsOutput(int Relayed);
