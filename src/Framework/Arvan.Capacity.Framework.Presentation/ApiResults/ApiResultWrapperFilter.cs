using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Arvan.Capacity.Framework.Presentation.Tracing;
using Arvan.Capacity.Framework.Presentation.Validation;

namespace Arvan.Capacity.Framework.Presentation.ApiResults;

/// <summary>
/// Wraps every action's return value in an <see cref="ApiResult{T}"/> on its way out, so that
/// controllers keep returning their own <c>Response</c> shapes and no endpoint can forget the
/// envelope.
/// </summary>
/// <remarks>
/// Failures reach the client by being thrown, not returned — <c>AppExceptionHandler</c> maps a
/// <c>BusinessException</c> or <c>DomainException</c> to its status and envelope (FRAMEWORK.md §7).
/// This filter therefore only has success payloads to wrap, plus one defensive case: a
/// <see cref="ProblemDetails"/> produced by the framework itself is converted rather than allowed
/// through, so no endpoint can emit the shape this envelope replaced.
/// </remarks>
internal sealed class ApiResultWrapperFilter : IAsyncResultFilter
{
    /// <inheritdoc cref="IAsyncResultFilter.OnResultExecutionAsync"/>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var traceId = context.HttpContext.GetTraceId();

        switch (context.Result)
        {
            // Already enveloped — the validation filter and the exception handler build their own.
            case ObjectResult { Value: IApiResult }:
                break;

            case ObjectResult { Value: ProblemDetails problemDetails } objectResult:
                Replace(objectResult, ApiResult.Fail(ToApiError(problemDetails), traceId));
                break;

            case ObjectResult objectResult:
                Replace(objectResult, new ApiResult<object> { Data = objectResult.Value, TraceId = traceId });
                break;

            // Ok(), NoContent() and friends: a bodiless success. Enveloped at 200 rather than left
            // as a 204, because a 204 carries no body and the envelope would then be conditional
            // on which verb the endpoint happened to use.
            case StatusCodeResult statusCodeResult when IsSuccessStatus(statusCodeResult.StatusCode):
                context.Result = new ObjectResult(ApiResult.Empty(traceId))
                {
                    StatusCode = StatusCodes.Status200OK,
                    DeclaredType = typeof(ApiResult<object>),
                };
                break;
        }

        await next();
    }

    /// <summary>
    /// Swaps an action's payload for its enveloped form in place, preserving the status code,
    /// content types and formatters the action selected.
    /// </summary>
    private static void Replace(ObjectResult objectResult, ApiResult<object> wrapped)
    {
        objectResult.Value = wrapped;

        // The declared type still names the unwrapped payload; leaving it would have the output
        // formatter serialize the envelope against the wrong contract.
        objectResult.DeclaredType = typeof(ApiResult<object>);
    }

    /// <summary>
    /// Converts a framework-produced <see cref="ProblemDetails"/> to the equivalent
    /// <see cref="ApiError"/>, mapping a <see cref="ValidationProblemDetails"/>'s per-field errors
    /// onto <see cref="ApiError.Details"/>.
    /// </summary>
    private static ApiError ToApiError(ProblemDetails problemDetails)
    {
        if (problemDetails is ValidationProblemDetails validation)
        {
            // Title here is a human sentence ("One or more validation errors occurred.") and
            // Detail is usually null, so neither can stand in for a machine-readable code.
            return new ApiError
            {
                Code = ApiErrorDefaults.ValidationFailedCode,
                Message = ApiErrorDefaults.ValidationFailedMessage,
                LocalizedMessage = ApiErrorDefaults.ValidationFailedLocalizedMessage,
                Details = validation.Errors
                    .SelectMany(entry => entry.Value.Select(message => new ApiErrorDetail(
                        WireFieldName.From(entry.Key),
                        ApiErrorDefaults.ValidationFailedCode,
                        message)))
                    .ToList(),
            };
        }

        return new ApiError
        {
            Code = problemDetails.Title ?? ApiErrorDefaults.InternalErrorCode,
            Message = problemDetails.Detail ?? problemDetails.Title ?? ApiErrorDefaults.InternalErrorMessage,
        };
    }

    /// <summary>
    /// Whether a bodiless result reports success and so should carry an empty envelope.
    /// </summary>
    private static bool IsSuccessStatus(int statusCode) => statusCode is >= 200 and < 300;
}
