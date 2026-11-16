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
using Arvan.Capacity.Framework.Presentation.ApiResults;
using Arvan.Capacity.Framework.Presentation.Tracing;
using Arvan.Capacity.Http;
using Arvan.Capacity.Mappers.Reservations;
using Arvan.Capacity.ViewModels.Reservations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arvan.Capacity.Controllers.Reservations;

/// <summary>
/// Reserving capacity: submit a request, track it, confirm or cancel it, extend it, and release it
/// when the service it backed is gone.
/// </summary>
/// <remarks>
/// Every call reads the identity the gateway set (<c>X-Customer-Id</c>, <c>X-Actor-Kind</c>,
/// <c>X-Actor-Id</c>), and every call that changes state requires an <c>Idempotency-Key</c>.
/// </remarks>
[ApiController]
[Route("v1/reservations")]
public sealed class ReservationsController(
    SubmitReservationUseCase submitReservation,
    GetReservationUseCase getReservation,
    ListReservationsUseCase listReservations,
    ConfirmReservationUseCase confirmReservation,
    CancelReservationUseCase cancelReservation,
    ExtendReservationUseCase extendReservation,
    ReleaseReservationUseCase releaseReservation) : ControllerBase
{
    /// <summary>
    /// Submits a reservation: every line is held, or none is.
    /// </summary>
    /// <remarks>
    /// A held reservation answers 201 with a <c>Location</c>. With <c>onShortage: Wait</c>, a
    /// shortage that is only temporary answers 202 with a waiting reservation. A refusal is stored,
    /// so it appears in history and its idempotency key replays it: 422 when no pool could ever
    /// provide the quantity, 409 otherwise, with the stored reservation in <c>data</c> and the
    /// reason in <c>error.code</c>; a temporary shortage carries <c>Retry-After</c>. Too many open
    /// holds or a cooldown is a 429. A replayed answer carries <c>Idempotent-Replayed: true</c>.
    /// </remarks>
    [HttpPost]
    [EnableRateLimiting("submit")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationResponse>> Submit(
        SubmitReservationRequest request, CancellationToken ct)
    {
        var caller = CallerHeaders.ReadCaller(Request);
        var key = CallerHeaders.ReadIdempotencyKey(Request);

        var output = await submitReservation.ExecuteAsync(request.ToInput(caller, key), ct);
        MarkReplay(output.Replayed);

        var response = output.Reservation.ToResponse();

        if (output.RetryAfterSeconds is not null)
            Response.Headers.RetryAfter = output.RetryAfterSeconds.Value.ToString();

        return output.Reservation.Status switch
        {
            ReservationStatus.Rejected => Rejected(response, output.Reservation.ReasonCode),
            ReservationStatus.Waiting => Accepted($"/v1/reservations/{response.Id}", response),
            _ => Created($"/v1/reservations/{response.Id}", response),
        };
    }

    /// <summary>
    /// Retrieves a reservation as it reads right now.
    /// </summary>
    /// <remarks>
    /// <c>status</c> is the effective status: a hold past its lease reads <c>Expired</c> with
    /// <c>settled: false</c> until the sweeper stores it. Another customer's reservation is a 404.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("reads")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> Get(Guid id, CancellationToken ct)
    {
        var summary = await getReservation.ExecuteAsync(id.ToGetInput(CallerHeaders.ReadCaller(Request)), ct);
        return Ok(summary.ToResponse());
    }

    /// <summary>
    /// Lists the caller's reservations, newest first.
    /// </summary>
    /// <remarks>
    /// <c>status</c> filters on the effective status and may repeat. <c>pageNumber</c> defaults to 1
    /// and <c>pageSize</c> to 20, at most 100.
    /// </remarks>
    [HttpGet]
    [EnableRateLimiting("reads")]
    [ProducesResponseType(typeof(PagedData<ReservationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedData<ReservationResponse>>> List(
        [FromQuery] ListReservationsRequest request, CancellationToken ct)
    {
        var page = await listReservations.ExecuteAsync(request.ToInput(CallerHeaders.ReadCaller(Request)), ct);
        return Ok(page.ToResponse());
    }

    /// <summary>
    /// Confirms a held reservation: its held capacity becomes allocated.
    /// </summary>
    /// <remarks>
    /// Confirming one that is already confirmed answers 200 with it, even with a new key. A hold
    /// past its lease is a 409 <c>reservation.expired</c>.
    /// </remarks>
    [HttpPost("{id:guid}/confirm")]
    [EnableRateLimiting("transitions")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Confirm(Guid id, CancellationToken ct)
    {
        var output = await confirmReservation.ExecuteAsync(
            id.ToConfirmInput(CallerHeaders.ReadCaller(Request), CallerHeaders.ReadIdempotencyKey(Request)), ct);

        MarkReplay(output.Replayed);
        return Ok(output.Reservation.ToResponse());
    }

    /// <summary>
    /// Cancels a held reservation: its held capacity returns at once.
    /// </summary>
    /// <remarks>
    /// Cancelling one that is already cancelled answers 200 with it, even with a new key.
    /// </remarks>
    [HttpPost("{id:guid}/cancel")]
    [EnableRateLimiting("transitions")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Cancel(Guid id, CancellationToken ct)
    {
        var output = await cancelReservation.ExecuteAsync(
            id.ToCancelInput(CallerHeaders.ReadCaller(Request), CallerHeaders.ReadIdempotencyKey(Request)), ct);

        MarkReplay(output.Replayed);
        return Ok(output.Reservation.ToResponse());
    }

    /// <summary>
    /// Extends a held reservation's lease once, by its product's TTL.
    /// </summary>
    /// <remarks>
    /// A second extension is a 409 <c>reservation.already_extended</c>; a hold past its lease is a
    /// 409 <c>reservation.expired</c>.
    /// </remarks>
    [HttpPost("{id:guid}/extend")]
    [EnableRateLimiting("transitions")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Extend(Guid id, CancellationToken ct)
    {
        var output = await extendReservation.ExecuteAsync(
            id.ToExtendInput(CallerHeaders.ReadCaller(Request), CallerHeaders.ReadIdempotencyKey(Request)), ct);

        MarkReplay(output.Replayed);
        return Ok(output.Reservation.ToResponse());
    }

    /// <summary>
    /// Gives confirmed capacity back, when the service it backed was deleted or never came up.
    /// </summary>
    /// <remarks>
    /// Only a service or an admin may call this; a customer gets 403. Releasing what is already
    /// released answers 200 with it.
    /// </remarks>
    [HttpPost("{id:guid}/release")]
    [EnableRateLimiting("transitions")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Release(
        Guid id, ReleaseReservationRequest request, CancellationToken ct)
    {
        var output = await releaseReservation.ExecuteAsync(
            request.ToInput(id, CallerHeaders.ReadCaller(Request), CallerHeaders.ReadIdempotencyKey(Request)), ct);

        MarkReplay(output.Replayed);
        return Ok(output.Reservation.ToResponse());
    }

    private void MarkReplay(bool replayed)
    {
        if (replayed)
            Response.Headers["Idempotent-Replayed"] = "true";
    }

    /// <summary>
    /// A stored refusal: the status line and the error say it failed, and the body still carries
    /// the stored reservation so the client has its id.
    /// </summary>
    private ObjectResult Rejected(ReservationResponse response, ReasonCode? reason)
    {
        var (code, message, status) = reason switch
        {
            ReasonCode.ExceedsRegisteredCapacity => (
                "capacity.exceeds_registered",
                "The request asks for more than any pool in scope can ever provide.",
                StatusCodes.Status422UnprocessableEntity),
            ReasonCode.QuotaExceeded => (
                "quota.exceeded",
                "The request would exceed the customer's contract limit, counting its open holds.",
                StatusCodes.Status409Conflict),
            ReasonCode.PoolShareExceeded => (
                "capacity.pool_share_exceeded",
                "The request would exceed the customer's share of a scarce pool.",
                StatusCodes.Status409Conflict),
            ReasonCode.CapacitySoldOut => (
                "capacity.sold_out",
                "The request fits registered capacity, but live allocations fill it.",
                StatusCodes.Status409Conflict),
            _ => (
                "capacity.temporarily_unavailable",
                "Capacity is short only because of open holds or an offline pool; retrying later can succeed.",
                StatusCodes.Status409Conflict),
        };

        return new ObjectResult(new ApiResult<ReservationResponse>
        {
            Data = response,
            Error = new ApiError { Code = code, Message = message },
            TraceId = HttpContext.GetTraceId(),
        })
        {
            StatusCode = status,
        };
    }
}
