using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Reservations;

/// <summary>
/// The JSON body for <c>POST /v1/reservations</c>.
/// </summary>
/// <param name="Product">The product code.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone, or omitted to let the service pick.</param>
/// <param name="Lines">One to sixteen resource lines, held all together or not at all.</param>
/// <param name="OnShortage">What to do when capacity is short; defaults to <c>Reject</c>.</param>
/// <param name="ClientReference">The client's own reference, unique among its live reservations.</param>
public sealed record SubmitReservationRequest(
    string Product,
    string Region,
    string? Zone,
    IReadOnlyList<SubmitReservationLineRequest> Lines,
    ShortagePolicy? OnShortage,
    string? ClientReference);

/// <summary>
/// One line of <see cref="SubmitReservationRequest"/>.
/// </summary>
/// <param name="ResourceClass">The class asked for.</param>
/// <param name="Quantity">The quantity in the class's base unit: millicores, megabytes, gigabytes or units.</param>
public sealed record SubmitReservationLineRequest(ResourceClass ResourceClass, long Quantity);
