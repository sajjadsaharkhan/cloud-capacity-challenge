using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Capacity;

/// <summary>
/// The JSON body for <c>POST /v1/capacity/fit-check</c>: a submit without its shortage policy and
/// client reference.
/// </summary>
/// <param name="Product">The product code.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone, or omitted.</param>
/// <param name="Lines">One to sixteen resource lines.</param>
public sealed record FitCheckRequest(string Product, string Region, string? Zone, IReadOnlyList<FitCheckLineRequest> Lines);

/// <summary>
/// One line of <see cref="FitCheckRequest"/>.
/// </summary>
/// <param name="ResourceClass">The class asked for.</param>
/// <param name="Quantity">The quantity in the class's base unit.</param>
public sealed record FitCheckLineRequest(ResourceClass ResourceClass, long Quantity);
