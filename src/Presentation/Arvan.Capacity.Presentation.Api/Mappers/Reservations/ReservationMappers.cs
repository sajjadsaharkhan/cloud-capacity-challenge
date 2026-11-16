using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Application.UseCases.Reservations.CancelReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ConfirmReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ExtendReservation;
using Arvan.Capacity.Application.UseCases.Reservations.GetReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ListReservations;
using Arvan.Capacity.Application.UseCases.Reservations.ReleaseReservation;
using Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Framework.Domain.Abstractions;
using Arvan.Capacity.Http;
using Arvan.Capacity.ViewModels.Reservations;

namespace Arvan.Capacity.Mappers.Reservations;

/// <summary>
/// Manual mappings between the reservation endpoints' wire shapes (<c>Request</c>,
/// <c>Response</c>) and the Application layer's <c>Input</c> and <c>Output</c> shapes. The caller's
/// identity and the idempotency key come from headers, so they join the body here rather than
/// travelling as client-settable fields.
/// </summary>
public static class ReservationMappers
{
    /// <summary>
    /// Maps a submit body and its headers to <see cref="SubmitReservationInput"/>.
    /// </summary>
    public static SubmitReservationInput ToInput(this SubmitReservationRequest request, Caller caller, string idempotencyKey) =>
        new(
            caller.CustomerId,
            caller.Actor,
            idempotencyKey,
            request.Product,
            request.Region,
            request.Zone,
            [.. request.Lines.Select(l => new SubmitReservationLineInput(l.ResourceClass, l.Quantity))],
            request.OnShortage ?? ShortagePolicy.Reject,
            request.ClientReference);

    /// <summary>
    /// Maps the route id and headers of a confirm call to <see cref="ConfirmReservationInput"/>.
    /// </summary>
    public static ConfirmReservationInput ToConfirmInput(this Guid id, Caller caller, string idempotencyKey) =>
        new(id, caller.CustomerId, caller.Actor, idempotencyKey);

    /// <summary>
    /// Maps the route id and headers of a cancel call to <see cref="CancelReservationInput"/>.
    /// </summary>
    public static CancelReservationInput ToCancelInput(this Guid id, Caller caller, string idempotencyKey) =>
        new(id, caller.CustomerId, caller.Actor, idempotencyKey);

    /// <summary>
    /// Maps the route id and headers of an extend call to <see cref="ExtendReservationInput"/>.
    /// </summary>
    public static ExtendReservationInput ToExtendInput(this Guid id, Caller caller, string idempotencyKey) =>
        new(id, caller.CustomerId, caller.Actor, idempotencyKey);

    /// <summary>
    /// Maps a release body, the route id and headers to <see cref="ReleaseReservationInput"/>.
    /// </summary>
    public static ReleaseReservationInput ToInput(
        this ReleaseReservationRequest request, Guid id, Caller caller, string idempotencyKey) =>
        new(id, caller.CustomerId, caller.Actor, idempotencyKey, request.Reason);

    /// <summary>
    /// Maps the route id and caller of a read to <see cref="GetReservationInput"/>.
    /// </summary>
    public static GetReservationInput ToGetInput(this Guid id, Caller caller) => new(id, caller.CustomerId);

    /// <summary>
    /// Maps a history query and the caller to <see cref="ListReservationsInput"/>.
    /// </summary>
    public static ListReservationsInput ToInput(this ListReservationsRequest request, Caller caller) =>
        new(caller.CustomerId, request.Status ?? [], request.Product, request.Region, request.PageNumber, request.PageSize);

    /// <summary>
    /// Maps a reservation projection to <see cref="ReservationResponse"/>.
    /// </summary>
    public static ReservationResponse ToResponse(this ReservationSummary summary) =>
        new(
            summary.Id,
            summary.Status,
            summary.Settled,
            summary.ReasonCode,
            summary.ProductCode,
            summary.Region,
            summary.Zone,
            [.. summary.Lines.Select(l => new ReservationLineResponse(l.ResourceClass, l.Quantity))],
            summary.ExpiresAt,
            summary.WaitDeadline,
            summary.Extended,
            summary.AllocationState,
            summary.ClientReference,
            summary.Version,
            summary.CreatedAt,
            summary.UpdatedAt);

    /// <summary>
    /// Maps a page of projections to a page of <see cref="ReservationResponse"/>.
    /// </summary>
    public static PagedData<ReservationResponse> ToResponse(this PagedData<ReservationSummary> page) =>
        page.Map(ToResponse);
}
