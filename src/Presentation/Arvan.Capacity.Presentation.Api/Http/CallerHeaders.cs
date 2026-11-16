using System.Text.RegularExpressions;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Constants;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Http;

/// <summary>
/// Reads the headers the gateway sets: the service trusts them, because the gateway has already
/// authenticated the caller.
/// </summary>
public static partial class CallerHeaders
{
    /// <summary>
    /// Reads the caller's identity.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <exception cref="IdentityMissingException">Thrown when a header is missing or the actor kind is not one of CUSTOMER, SERVICE, ADMIN.</exception>
    public static Caller ReadCaller(HttpRequest request)
    {
        var customerId = request.Headers["X-Customer-Id"].ToString();
        var actorKind = request.Headers["X-Actor-Kind"].ToString();
        var actorId = request.Headers["X-Actor-Id"].ToString();

        if (customerId.Length is 0
            || actorId.Length is 0
            || !Enum.TryParse<ActorKind>(actorKind, ignoreCase: true, out var kind)
            || kind is ActorKind.Sweeper)
            throw new IdentityMissingException();

        return new Caller(customerId, new Actor(kind, actorId));
    }

    /// <summary>
    /// Reads who is calling, for endpoints that concern no particular customer, such as the admin API.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <exception cref="IdentityMissingException">Thrown when a header is missing or the actor kind is not one of CUSTOMER, SERVICE, ADMIN.</exception>
    public static Actor ReadActor(HttpRequest request)
    {
        var actorKind = request.Headers["X-Actor-Kind"].ToString();
        var actorId = request.Headers["X-Actor-Id"].ToString();

        return actorId.Length is 0 || !Enum.TryParse<ActorKind>(actorKind, ignoreCase: true, out var kind) || kind is ActorKind.Sweeper
            ? throw new IdentityMissingException()
            : new Actor(kind, actorId);
    }

    /// <summary>
    /// Reads the <c>Idempotency-Key</c> of a state-changing call.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <exception cref="IdempotencyKeyRequiredException">Thrown when the header is missing, too long, or has characters outside <c>[A-Za-z0-9_-]</c>.</exception>
    public static string ReadIdempotencyKey(HttpRequest request)
    {
        var key = request.Headers["Idempotency-Key"].ToString();

        return key.Length is >= 1 and <= ReservationConstant.IdempotencyKeyMaxLength && KeyPattern().IsMatch(key)
            ? key
            : throw new IdempotencyKeyRequiredException();
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex KeyPattern();
}
