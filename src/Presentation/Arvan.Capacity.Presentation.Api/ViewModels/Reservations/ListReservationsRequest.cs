using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.ViewModels.Reservations;

/// <summary>
/// The query string for <c>GET /v1/reservations</c>.
/// </summary>
public sealed record ListReservationsRequest
{
    /// <summary>
    /// Effective statuses to keep; repeat the parameter for several. Omit for all.
    /// </summary>
    public ReservationStatus[]? Status { get; init; }

    /// <summary>
    /// Keep only this product.
    /// </summary>
    public string? Product { get; init; }

    /// <summary>
    /// Keep only this region.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// The 1-based page number. Defaults to 1.
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// The page size, 1 to 100. Defaults to 20.
    /// </summary>
    public int PageSize { get; init; } = 20;
}
