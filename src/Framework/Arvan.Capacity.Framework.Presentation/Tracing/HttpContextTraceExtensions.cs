using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Arvan.Capacity.Framework.Presentation.Tracing;

/// <summary>
/// Resolves the correlation id stamped onto every response envelope.
/// </summary>
public static class HttpContextTraceExtensions
{
    /// <summary>
    /// The current distributed-trace id, falling back to ASP.NET's per-request identifier when no
    /// <see cref="Activity"/> is running. This is the same source <c>ProblemDetails</c> uses for
    /// its own <c>traceId</c>, so log correlation is unchanged by the move off it.
    /// </summary>
    /// <param name="context">The request whose correlation id is wanted.</param>
    public static string GetTraceId(this HttpContext context) =>
        Activity.Current?.Id ?? context.TraceIdentifier;
}
