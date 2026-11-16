using Arvan.Capacity.Application;
using Arvan.Capacity.Framework.Presentation.Observability;
using Arvan.Capacity.Infrastructure;
using Arvan.Capacity.Infrastructure.Persistence;
using Arvan.Capacity.Worker;
using Arvan.Capacity.Worker.Workers;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddCapacityObservability("capacity-worker");

builder.Services.AddCapacityApplication();
builder.Services.AddCapacityPersistence(builder.Configuration);
builder.Services.AddCapacityGauges();

// The role decides what this instance runs: `sweeper`, `relay`, or both. One image, one build.
var roles = (builder.Configuration["Capacity:Roles"] ?? "sweeper,relay")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

if (roles.Contains("sweeper", StringComparer.OrdinalIgnoreCase))
    builder.Services.AddHostedService<SweeperWorker>();

if (roles.Contains("relay", StringComparer.OrdinalIgnoreCase))
    builder.Services.AddHostedService<RelayWorker>();

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
    await DatabaseMigrator.IsReadyAsync(dataSource, ct) ? Results.Ok() : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapCapacityMetrics();

app.Run();
