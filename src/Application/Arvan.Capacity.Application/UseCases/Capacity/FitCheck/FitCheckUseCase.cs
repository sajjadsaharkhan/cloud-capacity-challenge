using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Reservations;

namespace Arvan.Capacity.Application.UseCases.Capacity.FitCheck;

/// <summary>
/// Answers whether a request would be accepted right now, including the caller's own quota.
/// Corresponds to <c>POST /v1/capacity/fit-check</c>. A read: nothing is held, and the answer can
/// be stale by the time a submit arrives.
/// </summary>
public sealed class FitCheckUseCase(ICapacityReadonlyRepository capacity)
    : IUseCase<FitCheckInput, FitCheckOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    /// <exception cref="ProductNotAvailableException">Thrown when the product is unknown, retired, or does not allow a requested class.</exception>
    public async Task<FitCheckOutput> ExecuteAsync(FitCheckInput input, CancellationToken ct)
    {
        var lines = input.Lines.OrderBy(l => l.ResourceClass).Select(l => new ReservationLine(l.ResourceClass, l.Quantity)).ToList();
        var classes = lines.Select(l => l.ResourceClass).ToList();

        var product = (await capacity.GetProductsAsync(input.Product, ct)).FirstOrDefault();
        if (product is null || !product.Accepts(classes))
            throw new ProductNotAvailableException(input.Product);

        var snapshots = await capacity.GetSnapshotsAsync(input.Region, classes, ct);
        if (input.Zone is not null && !snapshots.Any(s => s.Zone == input.Zone))
            throw new ReservationZoneInvalidException(input.Zone, input.Region);

        foreach (var line in lines)
            snapshots.FirstOrDefault(s => s.ResourceClass == line.ResourceClass)?.EnsureQuantityAllowed(line.Quantity);

        var room = await capacity.GetQuotaRoomAsync(input.CustomerId, input.Region, classes, ct);
        var quotaShort = lines.Where(l => room[l.ResourceClass] < l.Quantity)
            .Select(l => new FitCheckLineOutput(l.ResourceClass, ReasonCode.QuotaExceeded)).ToList();

        if (quotaShort.Count > 0)
            return new FitCheckOutput(false, ReasonCode.QuotaExceeded, quotaShort);

        var plans = HoldPlanner.Plan(input.Zone, lines, snapshots);
        var feasible = plans.Any(plan => plan.Lines.All(a =>
            snapshots.First(s => s.PoolId == a.PoolId && s.ResourceClass == a.ResourceClass).Free >= a.Quantity));

        if (feasible)
            return new FitCheckOutput(true, null, []);

        var shortLines = lines
            .Select(l => (Line: l, Pools: snapshots.Where(s => s.ResourceClass == l.ResourceClass).ToList()))
            .Where(x => !x.Pools.Any(p => p.Free >= x.Line.Quantity))
            .Select(x => new FitCheckLineOutput(x.Line.ResourceClass, ShortageClassifier.Classify(x.Line.Quantity, x.Pools)))
            .ToList();

        var code = shortLines.Count is 0
            ? ReasonCode.CapacityTemporarilyUnavailable
            : ShortageClassifier.MostSevere(shortLines.Select(l => l.Code));

        return new FitCheckOutput(false, code, shortLines);
    }
}
