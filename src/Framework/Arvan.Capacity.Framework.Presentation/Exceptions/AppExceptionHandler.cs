using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Domain.Exceptions;
using Arvan.Capacity.Framework.Presentation.ApiResults;
using Arvan.Capacity.Framework.Presentation.Tracing;

namespace Arvan.Capacity.Framework.Presentation.Exceptions;

/// <summary>
/// Maps a <see cref="BusinessException"/> or <see cref="DomainException"/> escaping a UseCase to an
/// <see cref="ApiResult{T}"/> — <see cref="NotFoundException"/> to 404,
/// <see cref="UnauthorizedException"/> to 401, <see cref="ForbiddenException"/> to 403,
/// <see cref="UnprocessableException"/> to 422, <see cref="ConflictException"/> and any other <see cref="DomainException"/> (a Tier-0 guard fired
/// because of the aggregate's current state) to 409, <see cref="DomainValidationException"/> (a
/// Tier-0 guard rejected the input itself) and any other <see cref="BusinessException"/> to 400.
/// Anything else is logged and reported as a 500 that names nothing about the underlying exception.
/// </summary>
internal sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc cref="IExceptionHandler.TryHandleAsync"/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Once the response has started there is no envelope to write — the client is already
        // reading a body. Let it fall through and be logged by the framework.
        if (httpContext.Response.HasStarted)
            return false;

        var (error, statusCode) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                (exception as ServiceUnavailableException)?.Cause ?? exception,
                "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        var retryAfter = exception switch
        {
            TooManyRequestsException t => t.RetryAfterSeconds,
            ServiceUnavailableException u => u.RetryAfterSeconds,
            _ => (int?)null,
        };
        if (retryAfter is not null)
            httpContext.Response.Headers.RetryAfter = retryAfter.Value.ToString();

        await httpContext.Response.WriteAsJsonAsync(
            ApiResult.Fail(error, httpContext.GetTraceId()),
            cancellationToken);

        return true;
    }

    /// <summary>
    /// Derives the wire error and HTTP status from the exception's type. The status is not stored
    /// on the exception — it is fully determined by which type was thrown (FRAMEWORK.md §7).
    /// </summary>
    private static (ApiError Error, int StatusCode) Map(Exception exception) => exception switch
    {
        TooManyRequestsException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status429TooManyRequests),
        ServiceUnavailableException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status503ServiceUnavailable),
        NotFoundException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status404NotFound),
        UnauthorizedException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status401Unauthorized),
        ForbiddenException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status403Forbidden),
        UnprocessableException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status422UnprocessableEntity),
        ConflictException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status409Conflict),
        BusinessException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status400BadRequest),
        DomainValidationException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status400BadRequest),
        DomainException e => (ToError(e.Code, e.Message, e.LocalizedMessage), StatusCodes.Status409Conflict),
        _ => (
            ToError(
                ApiErrorDefaults.InternalErrorCode,
                ApiErrorDefaults.InternalErrorMessage,
                ApiErrorDefaults.InternalErrorLocalizedMessage),
            StatusCodes.Status500InternalServerError),
    };

    /// <summary>
    /// Builds an <see cref="ApiError"/>, omitting <see cref="ApiError.LocalizedMessage"/> from the
    /// payload entirely when the exception carried none.
    /// </summary>
    private static ApiError ToError(string code, string message, string? localizedMessage) =>
        new() { Code = code, Message = message, LocalizedMessage = localizedMessage };
}
