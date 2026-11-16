namespace Arvan.Capacity.Framework.Presentation.ApiResults;

/// <summary>
/// The non-generic face of <see cref="ApiResult{T}"/>, so that infrastructure which handles an
/// already-wrapped payload — the result filter deciding whether to wrap — can recognise one
/// without knowing its <c>Data</c> type.
/// </summary>
public interface IApiResult
{
    /// <summary>
    /// Whether the request succeeded. Derived from <see cref="Error"/> rather than stored, so it
    /// cannot contradict the error it reports.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    /// The failure that occurred, or <see langword="null"/> when the request succeeded.
    /// </summary>
    ApiError? Error { get; }

    /// <summary>
    /// The correlation id for this request, present on success and failure alike.
    /// </summary>
    string TraceId { get; }
}

/// <summary>
/// The envelope every API response is serialized as. Property declaration order here is the wire
/// order: <c>isSuccess</c>, <c>data</c>, <c>error</c>, <c>traceId</c>.
/// </summary>
/// <typeparam name="T">The payload type carried by <see cref="Data"/>.</typeparam>
public sealed record ApiResult<T> : IApiResult
{
    /// <inheritdoc cref="IApiResult.IsSuccess"/>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// The response payload, or <see langword="null"/> on failure and for operations that have
    /// nothing to report back.
    /// </summary>
    public T? Data { get; init; }

    /// <inheritdoc cref="IApiResult.Error"/>
    public ApiError? Error { get; init; }

    /// <inheritdoc cref="IApiResult.TraceId"/>
    public required string TraceId { get; init; }
}

/// <summary>
/// Factories for <see cref="ApiResult{T}"/>. The HTTP status code is set by the caller — this
/// envelope carries the semantic outcome, it does not replace the status line.
/// </summary>
public static class ApiResult
{
    /// <summary>
    /// A successful result carrying <paramref name="data"/>.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="data">The payload to return.</param>
    /// <param name="traceId">The correlation id for the current request.</param>
    public static ApiResult<T> Ok<T>(T data, string traceId) =>
        new() { Data = data, TraceId = traceId };

    /// <summary>
    /// A successful result with no payload, for an operation that has nothing to report back.
    /// Serializes as a null <c>data</c> rather than an empty body, so the envelope's shape does not
    /// depend on the verb.
    /// </summary>
    /// <param name="traceId">The correlation id for the current request.</param>
    public static ApiResult<object> Empty(string traceId) =>
        new() { TraceId = traceId };

    /// <summary>
    /// A failed result carrying <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The failure to report.</param>
    /// <param name="traceId">The correlation id for the current request.</param>
    public static ApiResult<object> Fail(ApiError error, string traceId) =>
        new() { Error = error, TraceId = traceId };
}
