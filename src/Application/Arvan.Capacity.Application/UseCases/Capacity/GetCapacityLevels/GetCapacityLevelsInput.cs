using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Capacity.GetCapacityLevels;

/// <summary>
/// The input to <see cref="GetCapacityLevelsUseCase"/>.
/// </summary>
/// <param name="ActorKind">Who is asking; only a service or an admin may ask for detail.</param>
/// <param name="Region">When set, only this region.</param>
/// <param name="Product">When set, only this product.</param>
/// <param name="Detail">Whether to include per-pool numbers.</param>
public sealed record GetCapacityLevelsInput(ActorKind ActorKind, string? Region, string? Product, bool Detail);
