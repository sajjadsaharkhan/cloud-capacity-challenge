using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Entities.Reservations;

/// <summary>
/// One transition of a <see cref="Reservation"/>, recorded as history and as the outbox row the
/// relay publishes. Appended in the same transaction as the transition itself.
/// </summary>
/// <param name="Version">The reservation's version after the transition.</param>
/// <param name="Event">The transition that happened.</param>
/// <param name="FromStatus">The stored status before, or <c>null</c> for the submit that created it.</param>
/// <param name="ToStatus">The stored status after.</param>
/// <param name="Actor">Who caused it.</param>
/// <param name="ReasonCode">Why, when the transition has a reason.</param>
/// <param name="At">The database time of the transition.</param>
public sealed record ReservationEvent(
    int Version,
    ReservationEventType Event,
    ReservationStatus? FromStatus,
    ReservationStatus ToStatus,
    Actor Actor,
    ReasonCode? ReasonCode,
    DateTimeOffset At)
{
    /// <summary>
    /// The routing key the event is published under.
    /// </summary>
    public string RoutingKey => (Event, ToStatus) switch
    {
        (ReservationEventType.Submit or ReservationEventType.Promote, ReservationStatus.Held) => "reservation.held",
        (ReservationEventType.Submit, ReservationStatus.Waiting) => "reservation.waiting",
        (ReservationEventType.Submit or ReservationEventType.Timeout, ReservationStatus.Rejected) => "reservation.rejected",
        (ReservationEventType.Extend, _) => "reservation.extended",
        (ReservationEventType.Confirm, _) => "reservation.confirmed",
        (ReservationEventType.Cancel, _) => "reservation.cancelled",
        (ReservationEventType.Expire, _) => "reservation.expired",
        (ReservationEventType.Release, _) => "reservation.released",
        _ => throw new InvalidOperationException($"No routing key for {Event} to {ToStatus}."),
    };
}
