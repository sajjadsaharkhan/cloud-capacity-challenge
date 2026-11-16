using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Capacity.GetCapacityLevels;

/// <summary>
/// Tells a customer how much capacity a product has in a region, as a level and never as numbers.
/// Corresponds to <c>GET /v1/capacity</c>. A read: no transaction.
/// </summary>
public sealed class GetCapacityLevelsUseCase(ICapacityReadonlyRepository capacity, IClock clock)
    : IUseCase<GetCapacityLevelsInput, GetCapacityLevelsOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ActorNotAllowedException">Thrown when a customer asks for per-pool detail.</exception>
    public async Task<GetCapacityLevelsOutput> ExecuteAsync(GetCapacityLevelsInput input, CancellationToken ct)
    {
        if (input.Detail && input.ActorKind is ActorKind.Customer)
            throw new ActorNotAllowedException("see per-pool capacity");

        var rows = await capacity.GetInventoryRowsAsync(input.Region, ct);
        var products = await capacity.GetProductsAsync(input.Product, ct);

        var items = new List<CapacityLevelOutput>();
        foreach (var product in products.Where(p => p.Status is ProductStatus.Active))
        foreach (var region in rows.Select(r => r.Region).Distinct().Order())
        {
            var levels = product.ResourceClasses.Select(resourceClass =>
            {
                var inRegion = rows.Where(r => r.Region == region && r.ResourceClass == resourceClass).ToList();
                return CapacityLevels.Of(inRegion.Sum(r => r.EffectiveFree), inRegion.Sum(r => r.Sellable));
            });

            items.Add(new CapacityLevelOutput(product.Id, region, CapacityLevels.Worst(levels)));
        }

        var pools = input.Detail
            ? rows.Select(r => new PoolCapacityOutput(
                r.Region, r.PoolId, r.Zone, r.HostRef, r.ResourceClass, r.Sellable, r.Held, r.Used, r.EffectiveFree)).ToList()
            : null;

        return new GetCapacityLevelsOutput(items, await clock.GetNowAsync(ct), Stale: false, pools);
    }
}
