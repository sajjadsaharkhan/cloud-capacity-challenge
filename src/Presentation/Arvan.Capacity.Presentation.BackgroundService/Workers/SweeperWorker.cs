using Arvan.Capacity.Application.UseCases.Admin.ReconcileLedger;
using Arvan.Capacity.Application.UseCases.Reservations.ExpireHolds;
using Arvan.Capacity.Application.UseCases.Reservations.PromoteWaiting;
using Arvan.Capacity.Application.UseCases.Reservations.PurgeIdempotencyKeys;
using Arvan.Capacity.Application.UseCases.Reservations.TimeOutWaiting;
using Microsoft.Extensions.Options;

namespace Arvan.Capacity.Worker.Workers;

/// <summary>
/// The sweeper role. Every second, in this order: store the end of every hold past its lease and
/// return its capacity; reject every wait past its deadline; give waiting requests the capacity
/// that came back. A backlog drains at full speed. Once a minute it purges stale idempotency keys,
/// and once an hour it computes the reconciliation report. Stateless, so any number may run at
/// once — locked rows are skipped.
/// </summary>
public sealed class SweeperWorker(IServiceScopeFactory scopes, IOptions<WorkerOptions> options, ILogger<SweeperWorker> logger)
    : BackgroundService
{
    private const int PurgeBatchSize = 1000;
    private const int PromoteBatchSize = 50;
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromHours(1);

    /// <inheritdoc cref="BackgroundService.ExecuteAsync"/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweeper = options.Value.Sweeper;
        var nextPurge = DateTimeOffset.MinValue;
        var nextReconcile = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Step("expire", () => Drain<ExpireHoldsUseCase, ExpireHoldsInput, ExpireHoldsOutput>(
                new ExpireHoldsInput(sweeper.BatchSize), o => o.Expired, sweeper.BatchSize, stoppingToken), stoppingToken);

            await Step("time out", () => Drain<TimeOutWaitingUseCase, TimeOutWaitingInput, TimeOutWaitingOutput>(
                new TimeOutWaitingInput(sweeper.BatchSize), o => o.TimedOut, sweeper.BatchSize, stoppingToken), stoppingToken);

            await Step("promote", () => Drain<PromoteWaitingUseCase, PromoteWaitingInput, PromoteWaitingOutput>(
                new PromoteWaitingInput(PromoteBatchSize), o => o.Promoted, PromoteBatchSize, stoppingToken), stoppingToken);

            if (DateTimeOffset.UtcNow >= nextPurge)
            {
                await Step("purge", () => Drain<PurgeIdempotencyKeysUseCase, PurgeIdempotencyKeysInput, PurgeIdempotencyKeysOutput>(
                    new PurgeIdempotencyKeysInput(PurgeBatchSize), o => o.Purged, PurgeBatchSize, stoppingToken), stoppingToken);
                nextPurge = DateTimeOffset.UtcNow + PurgeInterval;
            }

            if (DateTimeOffset.UtcNow >= nextReconcile)
            {
                await Step("reconcile", async () =>
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var drift = (await scope.ServiceProvider.GetRequiredService<ReconcileLedgerUseCase>()
                        .ExecuteAsync(new ReconcileLedgerInput(), stoppingToken)).DriftRows;

                    if (drift > 0)
                        logger.LogCritical("Reconciliation found {Drift} drifting counter(s); correct with a ledger adjustment", drift);
                }, stoppingToken);
                nextReconcile = DateTimeOffset.UtcNow + ReconcileInterval;
            }

            await Task.Delay(sweeper.IntervalMs, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <summary>
    /// Runs one step, so that one failing step does not stop the others: the next iteration retries
    /// the same rows.
    /// </summary>
    private async Task Step(string name, Func<Task> run, CancellationToken ct)
    {
        try
        {
            await run();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Sweeper step '{Step}' failed", name);
        }
    }

    /// <summary>
    /// Runs a UseCase in a fresh scope — its own connection and transaction — until a batch comes
    /// back short of full.
    /// </summary>
    private async Task Drain<TUseCase, TInput, TOutput>(TInput input, Func<TOutput, int> count, int batchSize, CancellationToken ct)
        where TUseCase : notnull, Arvan.Capacity.Application.Abstractions.IUseCase<TInput, TOutput>
    {
        int handled;
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            handled = count(await scope.ServiceProvider.GetRequiredService<TUseCase>().ExecuteAsync(input, ct));

            if (handled > 0)
                logger.LogInformation("{UseCase} handled {Count}", typeof(TUseCase).Name, handled);
        }
        while (handled == batchSize && !ct.IsCancellationRequested);
    }
}
