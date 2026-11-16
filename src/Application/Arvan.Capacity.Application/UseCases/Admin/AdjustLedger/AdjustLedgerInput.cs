using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Admin.AdjustLedger;

/// <summary>
/// The input to <see cref="AdjustLedgerUseCase"/>: either an inventory (<paramref name="PoolId"/>)
/// or a quota (<paramref name="CustomerId"/> and <paramref name="Region"/>).
/// </summary>
/// <param name="Actor">Who is calling; must be an admin.</param>
/// <param name="IdempotencyKey">The client's key.</param>
/// <param name="PoolId">The pool, for an inventory correction.</param>
/// <param name="CustomerId">The customer, for a quota correction.</param>
/// <param name="Region">The region, for a quota correction.</param>
/// <param name="ResourceClass">The class corrected.</param>
/// <param name="DeltaHeld">The change to <c>held</c>.</param>
/// <param name="DeltaUsed">The change to <c>used</c>.</param>
/// <param name="Reason">Why; required.</param>
public sealed record AdjustLedgerInput(
    Actor Actor, string IdempotencyKey, int? PoolId, string? CustomerId, string? Region,
    ResourceClass ResourceClass, long DeltaHeld, long DeltaUsed, string Reason);
