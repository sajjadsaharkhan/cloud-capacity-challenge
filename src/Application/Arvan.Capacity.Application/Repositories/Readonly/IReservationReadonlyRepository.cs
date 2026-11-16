using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Framework.Domain.Abstractions;

namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// The read-side port for <see cref="Reservation"/>. Every read goes through the
/// <c>reservation_effective</c> view, so a reader is never told a stale state, and every query is
/// scoped by customer.
/// </summary>
public interface IReservationReadonlyRepository : IReadonlyRepository<Reservation>
{
    /// <summary>
    /// Retrieves a customer's reservation, or <c>null</c> if it does not exist or is someone else's.
    /// </summary>
    /// <param name="id">The reservation's id.</param>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<ReservationSummary?> GetByIdAsync(Guid id, string customerId, CancellationToken ct);

    /// <summary>
    /// Retrieves one page of a customer's reservations, newest first.
    /// </summary>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="filter">The filters to apply.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The number of reservations per page.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<PagedData<ReservationSummary>> ListAsync(
        string customerId,
        ReservationListFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct);
}
