using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Arvan.Capacity.Framework.Presentation.Tracing;

namespace Arvan.Capacity.Framework.Presentation.ApiResults;

/// <summary>
/// Closes the last gap in the envelope: error responses the pipeline produces before MVC is
/// reached — an unmatched route, a wrong verb, an unsupported content type — carry no body at all,
/// so they would otherwise be the one class of response a client cannot parse uniformly.
/// </summary>
internal static class ApiResultStatusCodePages
{
    /// <summary>
    /// Writes an <see cref="ApiResult{T}"/> body for any error status that reached the client
    /// without one.
    /// </summary>
    /// <param name="app">The pipeline to add the handler to.</param>
    public static IApplicationBuilder UseApiResultStatusCodePages(this IApplicationBuilder app) =>
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var httpContext = statusCodeContext.HttpContext;
            var request = httpContext.Request;
            var (code, message) = Describe(httpContext.Response.StatusCode);
            message = $"{message} ({request.Method} {request.Path}{request.QueryString})";

            await httpContext.Response.WriteAsJsonAsync(
                ApiResult.Fail(
                    new ApiError { Code = code, Message = message },
                    httpContext.GetTraceId()),
                httpContext.RequestAborted);
        });

    /// <summary>
    /// Names the statuses the pipeline can emit on its own. These codes describe a transport-level
    /// failure and are deliberately distinct from a UseCase's domain codes — an
    /// <c>endpoint_not_found</c> is not an <c>organization.not_found</c>.
    /// </summary>
    private static (string Code, string Message) Describe(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => ("unauthorized", "Authentication is required."),
        StatusCodes.Status403Forbidden => ("forbidden", "This request is not permitted."),
        StatusCodes.Status404NotFound => ("endpoint_not_found", "No endpoint matches this route."),
        StatusCodes.Status405MethodNotAllowed => ("method_not_allowed", "This endpoint does not accept that method."),
        StatusCodes.Status415UnsupportedMediaType => ("unsupported_media_type", "This endpoint does not accept that content type."),
        _ => (ApiErrorDefaults.InternalErrorCode, ApiErrorDefaults.InternalErrorMessage),
    };
}
