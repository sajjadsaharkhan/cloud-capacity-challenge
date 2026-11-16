using Arvan.Capacity.Application.UseCases.Capacity.FitCheck;
using Arvan.Capacity.Application.UseCases.Capacity.GetCapacityLevels;
using Arvan.Capacity.Http;
using Arvan.Capacity.Mappers.Capacity;
using Arvan.Capacity.ViewModels.Capacity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arvan.Capacity.Controllers.Capacity;

/// <summary>
/// What a customer may see of capacity: a level per product and region, and an advisory check of
/// whether a request would be accepted now.
/// </summary>
[ApiController]
[Route("v1/capacity")]
public sealed class CapacityController(
    GetCapacityLevelsUseCase getCapacityLevels,
    FitCheckUseCase fitCheck) : ControllerBase
{
    /// <summary>
    /// Retrieves the capacity level of each product in each region.
    /// </summary>
    /// <remarks>
    /// The level is the worst across the product's resource classes: <c>Unavailable</c> when no pool
    /// has free capacity, <c>Limited</c> below 10% of what can be sold, otherwise <c>Available</c>.
    /// Holds past their lease already count as free. A service or an admin may pass
    /// <c>detail=true</c> for per-pool numbers; a customer who does gets a 403.
    /// </remarks>
    [HttpGet]
    [EnableRateLimiting("reads")]
    [ProducesResponseType(typeof(CapacityLevelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CapacityLevelsResponse>> GetLevels(
        [FromQuery] string? region, [FromQuery] string? product, [FromQuery] bool detail, CancellationToken ct)
    {
        var actor = CallerHeaders.ReadActor(Request);
        var output = await getCapacityLevels.ExecuteAsync(new GetCapacityLevelsInput(actor.Kind, region, product, detail), ct);
        return Ok(output.ToResponse());
    }

    /// <summary>
    /// Answers whether a request would be accepted now, including the caller's own quota.
    /// </summary>
    /// <remarks>
    /// Advisory only: nothing is held, and only a hold guarantees capacity. The answer never states
    /// how much is free. Rate-limited per customer.
    /// </remarks>
    [HttpPost("fit-check")]
    [EnableRateLimiting("fitcheck")]
    [ProducesResponseType(typeof(FitCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FitCheckResponse>> FitCheck(FitCheckRequest request, CancellationToken ct)
    {
        var output = await fitCheck.ExecuteAsync(request.ToInput(CallerHeaders.ReadCaller(Request)), ct);
        return Ok(output.ToResponse());
    }
}
