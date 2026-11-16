using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// The read projection for <see cref="Reservation"/> — what a reader is told at the moment of
/// reading. <see cref="Status"/> is the effective status, not the stored one. Pool identifiers
/// never appear: customers see the zone only.
/// </summary>
/// <param name="Id">The reservation's id.</param>
/// <param name="Status">The effective status at the moment of reading.</param>
/// <param name="Settled">Whether the stored state has caught up with <paramref name="Status"/>.</param>
/// <param name="ReasonCode">The effective reason, when the reservation has one.</param>
/// <param name="ProductCode">The product reserved.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone the zone-scoped lines were held in.</param>
/// <param name="Lines">The lines requested.</param>
/// <param name="ExpiresAt">The lease deadline of a hold.</param>
/// <param name="WaitDeadline">The deadline of a wait.</param>
/// <param name="Extended">Whether the lease was extended once.</param>
/// <param name="AllocationState">Whether confirmed capacity is still allocated.</param>
/// <param name="ClientReference">The client's own reference.</param>
/// <param name="Version">The reservation's version.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record ReservationSummary(
    Guid Id,
    ReservationStatus Status,
    bool Settled,
    ReasonCode? ReasonCode,
    string ProductCode,
    string Region,
    string? Zone,
    IReadOnlyList<ReservationLineSummary> Lines,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? WaitDeadline,
    bool Extended,
    AllocationState? AllocationState,
    string? ClientReference,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// One line of a <see cref="ReservationSummary"/>.
/// </summary>
/// <param name="ResourceClass">The class asked for.</param>
/// <param name="Quantity">The quantity in the class's base unit.</param>
public sealed record ReservationLineSummary(ResourceClass ResourceClass, long Quantity);
