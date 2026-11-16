namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when the identity headers the gateway must set are missing or malformed.
/// </summary>
public sealed class IdentityMissingException()
    : UnauthorizedException(
        "identity.missing",
        "Headers 'X-Customer-Id', 'X-Actor-Kind' and 'X-Actor-Id' are required, and the actor kind must be CUSTOMER, SERVICE or ADMIN.");
