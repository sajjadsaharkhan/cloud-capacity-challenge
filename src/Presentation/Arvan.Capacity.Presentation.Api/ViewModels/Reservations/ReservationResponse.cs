using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Reservations;

/// <summary>
/// A reservation as a reader sees it at the moment of reading.
/// </summary>
/// <param name="Id">The reservation's id.</param>
/// <param name="Status">The effective status: a hold past its lease already reads <c>Expired</c>.</param>
/// <param name="Settled">Whether the stored state has caught up with <paramref name="Status"/>.</param>
/// <param name="ReasonCode">Why the reservation reached its state, when it has a reason.</param>
/// <param name="Product">The product reserved.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone the capacity was held in.</param>
/// <param name="Lines">The lines requested.</param>
/// <param name="ExpiresAt">The lease deadline of a hold.</param>
/// <param name="WaitDeadline">The deadline of a wait.</param>
/// <param name="Extended">Whether the lease was extended once.</param>
/// <param name="AllocationState">Whether confirmed capacity is still allocated.</param>
/// <param name="ClientReference">The client's own reference.</param>
/// <param name="Version">The reservation's version.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record ReservationResponse(
    Guid Id,
    ReservationStatus Status,
    bool Settled,
    ReasonCode? ReasonCode,
    string Product,
    string Region,
    string? Zone,
    IReadOnlyList<ReservationLineResponse> Lines,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? WaitDeadline,
    bool Extended,
    AllocationState? AllocationState,
    string? ClientReference,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// One line of a <see cref="ReservationResponse"/>.
/// </summary>
/// <param name="ResourceClass">The class asked for.</param>
/// <param name="Quantity">The quantity in the class's base unit.</param>
public sealed record ReservationLineResponse(ResourceClass ResourceClass, long Quantity);
