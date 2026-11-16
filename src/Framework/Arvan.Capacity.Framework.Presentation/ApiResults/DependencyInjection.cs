using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Arvan.Capacity.Framework.Presentation.ApiResults;

/// <summary>
/// Registers the filter that wraps every action's return value in the response envelope.
/// </summary>
internal static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="ApiResultWrapperFilter"/> through <see cref="MvcOptions"/> rather than
    /// inside <c>AddControllers</c>, so a host gets it whichever order it makes its own calls in.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    internal static IServiceCollection AddApiResultWrapper(this IServiceCollection services) =>
        services.Configure<MvcOptions>(options => options.Filters.Add<ApiResultWrapperFilter>());
}
