using System.Threading.RateLimiting;
using Arvan.Capacity.Application;
using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Framework.Presentation;
using Arvan.Capacity.Framework.Presentation.ApiResults;
using Arvan.Capacity.Framework.Presentation.Observability;
using Arvan.Capacity.Framework.Presentation.Tracing;
using Arvan.Capacity.Infrastructure;
using Arvan.Capacity.Infrastructure.Persistence;
using FluentValidation;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddCapacityObservability("capacity-api");

// A request body is at most 16 KB: a reservation carries at most 16 short lines.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);

builder.Services.AddCapacityApplication();
builder.Services.AddCapacityPersistence(builder.Configuration);
builder.Services.AddCapacityGauges();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddOpenApi();

builder.Services.AddCapacityPresentation();
builder.Services.AddControllers();

// Token bucket per customer and endpoint group, per instance. With several instances the limits
// multiply; a shared limiter is the growth step when exact global limits are needed.
builder.Services.AddRateLimiter(options =>
{
    static string Key(HttpContext context) =>
        context.Request.Headers["X-Customer-Id"].FirstOrDefault()
        ?? context.Request.Headers["X-Actor-Id"].FirstOrDefault()
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    void Group(string name, int ratePerSecond, int burst) =>
        options.AddPolicy(name, context => RateLimitPartition.GetTokenBucketLimiter(
            $"{name}:{Key(context)}",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = burst,
                TokensPerPeriod = ratePerSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    Group("submit", 10, 20);
    Group("fitcheck", 5, 10);
    Group("reads", 50, 100);
    Group("transitions", 20, 40);

    options.OnRejected = async (context, ct) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? (int)Math.Ceiling(delay.TotalSeconds) : 1;
        var rejection = new RateLimitedException(retryAfter);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResult.Fail(new ApiError { Code = rejection.Code, Message = rejection.Message }, context.HttpContext.GetTraceId()), ct);
    };
});

var app = builder.Build();

var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var migrate = args.Contains("migrate");

if (migrate || database.Migrate)
    DatabaseMigrator.Migrate(
        builder.Configuration.GetConnectionString("Database")!,
        database.SeedDemoData,
        app.Services.GetRequiredService<ILogger<Program>>());

// `Arvan.Capacity.Presentation.Api.dll migrate` applies the migrations and exits: the migrator
// service of the compose file, and the Kubernetes Job before a rollout.
if (migrate)
    return;

app.MapOpenApi();

app.UseCapacityPresentation();
app.UseRateLimiter();

app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
    await DatabaseMigrator.IsReadyAsync(dataSource, ct) ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapCapacityMetrics();

app.MapControllers();

app.Run();

/// <summary>
/// Marks the entry point for the integration tests' <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
