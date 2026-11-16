using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Arvan.Capacity.Framework.Presentation.Json;

/// <summary>
/// Registers the wire JSON rules with the serializers a request or response travels through.
/// </summary>
internal static class DependencyInjection
{
    /// <summary>
    /// Applies one JSON configuration to both serializers an enveloped response can travel
    /// through.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    internal static IServiceCollection AddWireJson(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(options => WireJson.Apply(options.JsonSerializerOptions));

        services.ConfigureHttpJsonOptions(options => WireJson.Apply(options.SerializerOptions));

        return services;
    }
}
