using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Arvan.Capacity.Framework.Presentation.ApiResults;
using Arvan.Capacity.Framework.Presentation.Exceptions;
using Arvan.Capacity.Framework.Presentation.Json;
using Arvan.Capacity.Framework.Presentation.Tracing;
using Arvan.Capacity.Framework.Presentation.Validation;

namespace Arvan.Capacity.Framework.Presentation;

/// <summary>
/// The composition root for the presentation framework — every host wires the response envelope
/// through these two calls and names nothing else.
/// </summary>
/// <remarks>
/// The envelope only holds if a host registers all of its parts. Registering them together is what
/// keeps one host's validation responses from silently diverging from another's.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the response envelope: its JSON settings, the exception handler, the model
    /// binding failure path, and the MVC filters that wrap and validate.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddCapacityPresentation(this IServiceCollection services)
    {
        services.AddAppExceptionHandler();
        services.AddWireJson();
        services.AddRequestValidation();
        services.AddApiResultWrapper();

        return services;
    }

    /// <summary>
    /// Stamps the envelope's trace id onto logs and adds the pipeline stages that envelope a
    /// failure.
    /// </summary>
    /// <param name="app">The application to configure.</param>
    public static WebApplication UseCapacityPresentation(this WebApplication app)
    {
        app.UseTraceId();

        app.UseExceptionHandler();

        app.UseApiResultStatusCodePages();

        return app;
    }
}
