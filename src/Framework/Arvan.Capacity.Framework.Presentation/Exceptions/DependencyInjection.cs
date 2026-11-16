using Microsoft.Extensions.DependencyInjection;

namespace Arvan.Capacity.Framework.Presentation.Exceptions;

/// <summary>
/// Registers the exception handler that turns an escaping exception into the response envelope.
/// </summary>
internal static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="AppExceptionHandler"/> and the ProblemDetails service it depends on.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    internal static IServiceCollection AddAppExceptionHandler(this IServiceCollection services)
    {
        // UseExceptionHandler() refuses to start without either an explicit path or a registered
        // IProblemDetailsService, even though AppExceptionHandler intercepts before the fallback.
        services.AddProblemDetails();
        services.AddExceptionHandler<AppExceptionHandler>();

        return services;
    }
}
