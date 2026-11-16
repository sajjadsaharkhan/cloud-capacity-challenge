using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Arvan.Capacity.Framework.Presentation.ApiResults;
using Arvan.Capacity.Framework.Presentation.Tracing;

namespace Arvan.Capacity.Framework.Presentation.Validation;

/// <summary>
/// Registers request validation: the FluentValidation filter and the model-binding failure path,
/// so both report a failure in the same envelope.
/// </summary>
internal static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="FluentValidationActionFilter"/> and replaces the model-binding failure
    /// response with the envelope.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    internal static IServiceCollection AddRequestValidation(this IServiceCollection services)
    {
        // Registered through MvcOptions rather than inside AddControllers, so a host gets the filter
        // whichever order it makes its own calls in.
        services.Configure<MvcOptions>(options => options.Filters.Add<FluentValidationActionFilter>());

        services.AddModelStateEnvelope();

        return services;
    }

    /// <summary>
    /// Replaces the framework's <see cref="ValidationProblemDetails"/> response for a
    /// model-binding failure with the envelope.
    /// </summary>
    /// <remarks>
    /// Model binding fails before any action filter runs, so <c>FluentValidationActionFilter</c>
    /// never sees a malformed body or an unparseable route value. Without this a host would report
    /// those in a different shape from every other validation failure.
    /// </remarks>
    private static void AddModelStateEnvelope(this IServiceCollection services) =>
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
                ApiResult.Fail(
                    new ApiError
                    {
                        Code = ApiErrorDefaults.ValidationFailedCode,
                        Message = ApiErrorDefaults.ValidationFailedMessage,
                        LocalizedMessage = ApiErrorDefaults.ValidationFailedLocalizedMessage,
                        Details =
                        [
                            .. context.ModelState
                                .SelectMany(entry => entry.Value?.Errors.Select(error => new ApiErrorDetail(
                                    WireFieldName.From(entry.Key),
                                    ApiErrorDefaults.ValidationFailedCode,
                                    error.ErrorMessage)) ?? [])
                        ],
                    },
                    context.HttpContext.GetTraceId())));
}
