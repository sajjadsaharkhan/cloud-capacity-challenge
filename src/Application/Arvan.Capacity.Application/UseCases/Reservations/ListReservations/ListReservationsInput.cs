using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.UseCases.Reservations.ListReservations;

/// <summary>
/// The input to <see cref="ListReservationsUseCase"/>.
/// </summary>
/// <param name="CustomerId">The customer whose history is listed.</param>
/// <param name="Statuses">Effective statuses to keep; empty keeps all.</param>
/// <param name="ProductCode">Keep only this product.</param>
/// <param name="Region">Keep only this region.</param>
/// <param name="PageNumber">The 1-based page number.</param>
/// <param name="PageSize">The number of reservations per page.</param>
public sealed record ListReservationsInput(
    string CustomerId,
    IReadOnlyCollection<ReservationStatus> Statuses,
    string? ProductCode,
    string? Region,
    int PageNumber,
    int PageSize);
