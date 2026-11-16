using Arvan.Capacity.Application.UseCases.Capacity.FitCheck;
using Arvan.Capacity.Application.UseCases.Capacity.GetCapacityLevels;
using Arvan.Capacity.Http;
using Arvan.Capacity.ViewModels.Capacity;

namespace Arvan.Capacity.Mappers.Capacity;

/// <summary>
/// Manual mappings between the capacity endpoints' wire shapes and the Application layer's shapes.
/// </summary>
public static class CapacityMappers
{
    /// <summary>
    /// Maps a fit-check body and the caller to <see cref="FitCheckInput"/>.
    /// </summary>
    public static FitCheckInput ToInput(this FitCheckRequest request, Caller caller) =>
        new(caller.CustomerId, request.Product, request.Region, request.Zone,
            [.. request.Lines.Select(l => new FitCheckLineInput(l.ResourceClass, l.Quantity))]);

    /// <summary>
    /// Maps a fit-check result to <see cref="FitCheckResponse"/>.
    /// </summary>
    public static FitCheckResponse ToResponse(this FitCheckOutput output) =>
        new(output.Fits, output.Code, [.. output.Lines.Select(l => new FitCheckLineResponse(l.ResourceClass, l.Code))]);

    /// <summary>
    /// Maps capacity levels to <see cref="CapacityLevelsResponse"/>.
    /// </summary>
    public static CapacityLevelsResponse ToResponse(this GetCapacityLevelsOutput output) =>
        new(
            [.. output.Items.Select(i => new CapacityLevelResponse(i.Product, i.Region, i.Level))],
            output.AsOf,
            output.Stale,
            output.Pools?.Select(p => new PoolCapacityResponse(
                p.Region, p.PoolId, p.Zone, p.HostRef, p.ResourceClass, p.Sellable, p.Held, p.Used, p.Free)).ToList());
}
