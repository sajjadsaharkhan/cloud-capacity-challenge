namespace Arvan.Capacity.Framework.Presentation.ApiResults;

/// <summary>
/// The codes and messages for the two failures the framework raises itself, rather than a UseCase
/// raising them: a request that fails shape validation, and an exception no handler recognised.
/// </summary>
internal static class ApiErrorDefaults
{
    /// <summary>
    /// The <see cref="ApiError.Code"/> for a request that failed shape validation.
    /// </summary>
    public const string ValidationFailedCode = "validation_failed";

    /// <summary>
    /// The <see cref="ApiError.Message"/> for a request that failed shape validation.
    /// </summary>
    public const string ValidationFailedMessage = "One or more fields are invalid.";

    /// <summary>
    /// The <see cref="ApiError.LocalizedMessage"/> for a request that failed shape validation.
    /// </summary>
    public const string ValidationFailedLocalizedMessage = "اطلاعات ارسالی معتبر نیست.";

    /// <summary>
    /// The <see cref="ApiError.Code"/> for an unhandled exception.
    /// </summary>
    public const string InternalErrorCode = "internal_error";

    /// <summary>
    /// The <see cref="ApiError.Message"/> for an unhandled exception. Deliberately says nothing
    /// about the underlying exception — <see cref="ApiResult{T}.TraceId"/> is the only handle the
    /// client gets, and the only one support needs.
    /// </summary>
    public const string InternalErrorMessage = "An unexpected error occurred.";

    /// <summary>
    /// The <see cref="ApiError.LocalizedMessage"/> for an unhandled exception.
    /// </summary>
    public const string InternalErrorLocalizedMessage = "خطای غیرمنتظره‌ای رخ داد. لطفاً دوباره تلاش کنید.";
}
