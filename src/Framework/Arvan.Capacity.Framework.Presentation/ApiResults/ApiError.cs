using System.Text.Json.Serialization;

namespace Arvan.Capacity.Framework.Presentation.ApiResults;

/// <summary>
/// The failure carried by <see cref="ApiResult{T}.Error"/>. <see cref="Code"/> and
/// <see cref="LocalizedMessage"/> map directly onto <c>BusinessException</c>'s members of the same
/// name; the HTTP status is derived from the exception's type and lives on the status line, not
/// here.
/// </summary>
public sealed record ApiError
{
    /// <summary>
    /// The stable, machine-readable failure code — <c>organization.code_taken</c>. Clients branch
    /// on this, never on <see cref="Message"/>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The developer-facing English description of the failure. Never shown to an end user.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The end-user-facing message, when the failure has one. Absent from the payload rather than
    /// null when it does not.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalizedMessage { get; init; }

    /// <summary>
    /// The per-field breakdown of a validation failure. Absent from the payload rather than null
    /// for every other kind of failure.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ApiErrorDetail>? Details { get; init; }
}

/// <summary>
/// One field-level validation failure within <see cref="ApiError.Details"/>.
/// </summary>
/// <param name="Field">The name of the offending field, as the client sent it.</param>
/// <param name="Code">The rule that rejected it, for example <c>NotEmptyValidator</c>.</param>
/// <param name="Message">The developer-facing description of why it was rejected.</param>
public sealed record ApiErrorDetail(string Field, string Code, string Message);
