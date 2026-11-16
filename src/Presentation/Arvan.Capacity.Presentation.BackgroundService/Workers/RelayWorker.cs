using Arvan.Capacity.Application.UseCases.Events.RelayEvents;
using Microsoft.Extensions.Options;

namespace Arvan.Capacity.Worker.Workers;

/// <summary>
/// The relay role: publishes the outbox to the broker every 200 ms, looping while batches are full.
/// If it dies between publishing and committing, the batch is published again, so delivery is at
/// least once and consumers deduplicate by event id.
/// </summary>
public sealed class RelayWorker(IServiceScopeFactory scopes, IOptions<WorkerOptions> options, ILogger<RelayWorker> logger)
    : BackgroundService
{
    /// <inheritdoc cref="BackgroundService.ExecuteAsync"/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var relay = options.Value.Relay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int relayed;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    relayed = (await scope.ServiceProvider.GetRequiredService<RelayEventsUseCase>()
                        .ExecuteAsync(new RelayEventsInput(relay.BatchSize), stoppingToken)).Relayed;
                }
                while (relayed == relay.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // A broker outage delays events; it never blocks a reservation. The batch stays in the outbox.
                logger.LogError(e, "Relay iteration failed");
            }

            await Task.Delay(relay.IntervalMs, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
