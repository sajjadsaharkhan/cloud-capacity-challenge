using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Reservations;

/// <summary>
/// The JSON body for <c>POST /v1/reservations/{id}/release</c>.
/// </summary>
/// <param name="Reason">Why the capacity is released.</param>
public sealed record ReleaseReservationRequest(ReleaseReason Reason);
