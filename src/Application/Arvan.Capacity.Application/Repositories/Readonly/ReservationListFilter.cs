using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// The optional filters of a customer's reservation history.
/// </summary>
/// <param name="Statuses">Effective statuses to keep; empty keeps all.</param>
/// <param name="ProductCode">Keep only this product.</param>
/// <param name="Region">Keep only this region.</param>
public sealed record ReservationListFilter(
    IReadOnlyCollection<ReservationStatus> Statuses,
    string? ProductCode,
    string? Region);
