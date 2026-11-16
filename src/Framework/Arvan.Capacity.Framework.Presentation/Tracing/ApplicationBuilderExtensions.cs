using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Arvan.Capacity.Framework.Presentation.Tracing;

/// <summary>
/// Provides extension methods for correlating logs with a request.
/// </summary>
internal static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Stamps the request's <c>traceId</c> — the value
    /// <see cref="HttpContextTraceExtensions.GetTraceId"/> puts on the response envelope, under the
    /// same name — onto every log event written during the request.
    /// </summary>
    /// <remarks>
    /// Captured once at the start of the request so a log written inside a child span still carries
    /// the envelope's value. Must run before <c>UseExceptionHandler</c>, or the error logged for a
    /// 500 is written outside the scope.
    /// </remarks>
    /// <param name="app">The application builder to add the middleware to.</param>
    internal static void UseTraceId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");

            using (logger.BeginScope(new Dictionary<string, object> { ["traceId"] = context.GetTraceId() }))
                await next(context);
        });
}
