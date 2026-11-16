using Arvan.Capacity.Application.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Arvan.Capacity.Framework.Presentation.Observability;

/// <summary>
/// Wires the three signals: structured JSON logs to stdout, OpenTelemetry traces and metrics, and a
/// Prometheus scrape endpoint at <c>/metrics</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers JSON console logging, the service's meter and activity source, the ASP.NET Core and
    /// Npgsql instrumentation, and the Prometheus exporter. When <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
    /// is set, traces and metrics are also exported over OTLP, to a collector.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="serviceName">The name the signals carry, such as <c>capacity-api</c>.</param>
    public static WebApplicationBuilder AddCapacityObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddMeter(CapacityTelemetry.Name)
                .AddAspNetCoreInstrumentation()
                .AddPrometheusExporter())
            .WithTracing(tracing => tracing
                .AddSource(CapacityTelemetry.Name)
                .AddAspNetCoreInstrumentation()
                .AddSource("Npgsql"));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            telemetry.UseOtlpExporter();

        return builder;
    }

    /// <summary>
    /// Maps the Prometheus scrape endpoint at <c>/metrics</c>.
    /// </summary>
    /// <param name="app">The application to configure.</param>
    public static IEndpointConventionBuilder MapCapacityMetrics(this WebApplication app) =>
        app.MapPrometheusScrapingEndpoint("/metrics");
}
