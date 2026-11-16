using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;

/// <summary>
/// The input to <see cref="SubmitReservationUseCase"/>.
/// </summary>
/// <param name="CustomerId">The customer the capacity is reserved for.</param>
/// <param name="Actor">Who is submitting.</param>
/// <param name="IdempotencyKey">The client's key; a retry with the same key and body returns the first result.</param>
/// <param name="Product">The product code.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone, or <c>null</c> to let the service pick.</param>
/// <param name="Lines">The resource lines, held all together or not at all.</param>
/// <param name="OnShortage">What to do when capacity is short.</param>
/// <param name="ClientReference">The client's own reference, unique per customer among live reservations.</param>
public sealed record SubmitReservationInput(
    string CustomerId,
    Actor Actor,
    string IdempotencyKey,
    string Product,
    string Region,
    string? Zone,
    IReadOnlyList<SubmitReservationLineInput> Lines,
    ShortagePolicy OnShortage,
    string? ClientReference);

/// <summary>
/// One line of <see cref="SubmitReservationInput"/>.
/// </summary>
/// <param name="ResourceClass">The class asked for.</param>
/// <param name="Quantity">The quantity in the class's base unit.</param>
public sealed record SubmitReservationLineInput(ResourceClass ResourceClass, long Quantity);
