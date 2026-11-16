using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Capacity.FitCheck;

/// <summary>
/// The input to <see cref="FitCheckUseCase"/>: a submit without its shortage policy and reference.
/// </summary>
/// <param name="CustomerId">The customer whose quota is checked.</param>
/// <param name="Product">The product code.</param>
/// <param name="Region">The region.</param>
/// <param name="Zone">The zone, or <c>null</c>.</param>
/// <param name="Lines">The resource lines.</param>
public sealed record FitCheckInput(
    string CustomerId, string Product, string Region, string? Zone, IReadOnlyList<FitCheckLineInput> Lines);

/// <summary>
/// One line of <see cref="FitCheckInput"/>.
/// </summary>
/// <param name="ResourceClass">The class asked for.</param>
/// <param name="Quantity">The quantity in the class's base unit.</param>
public sealed record FitCheckLineInput(ResourceClass ResourceClass, long Quantity);
