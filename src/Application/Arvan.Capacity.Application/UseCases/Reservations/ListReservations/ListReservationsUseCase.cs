using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Framework.Domain.Abstractions;

namespace Arvan.Capacity.Application.UseCases.Reservations.ListReservations;

/// <summary>
/// Lists a customer's reservations, newest first. Corresponds to <c>GET /v1/reservations</c>.
/// A read: one readonly repository, no transaction.
/// </summary>
public sealed class ListReservationsUseCase(IReservationReadonlyRepository reservations)
    : IUseCase<ListReservationsInput, PagedData<ReservationSummary>>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public Task<PagedData<ReservationSummary>> ExecuteAsync(ListReservationsInput input, CancellationToken ct) =>
        reservations.ListAsync(
            input.CustomerId,
            new ReservationListFilter(input.Statuses, input.ProductCode, input.Region),
            input.PageNumber,
            input.PageSize,
            ct);
}
