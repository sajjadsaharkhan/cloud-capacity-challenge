using Arvan.Capacity.Domain.Entities.Capacity;

namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// What the capacity ledger needs to take a reservation's lines: who is asking, where, and from
/// which pools. The reservation row exists already, so the ledger rows can reference it.
/// </summary>
/// <param name="ReservationId">The reservation.</param>
/// <param name="CustomerId">The customer whose quota is charged.</param>
/// <param name="Region">The region.</param>
/// <param name="Lines">One allocation per line.</param>
/// <param name="MaxPoolSharePct">The product's share cap in percent; 100 turns it off.</param>
public sealed record LedgerHold(
    Guid ReservationId,
    string CustomerId,
    string Region,
    IReadOnlyList<LineAllocation> Lines,
    int MaxPoolSharePct);
