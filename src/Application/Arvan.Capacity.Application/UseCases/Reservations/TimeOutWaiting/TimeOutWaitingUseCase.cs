using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Operations.Reservations;

namespace Arvan.Capacity.Application.UseCases.Reservations.TimeOutWaiting;

/// <summary>
/// Stores the end of every wait that ran past its deadline: the request is rejected with
/// <c>WaitTimeout</c>. A wait holds no capacity, so nothing is returned. Triggered by the sweeper.
/// </summary>
public sealed class TimeOutWaitingUseCase(
    ITransactionManager transactionManager, IClock clock, IReservationOperations reservations)
    : IUseCase<TimeOutWaitingInput, TimeOutWaitingOutput>
{
    /// <inheritdoc cref="IUseCase{TInput,TOutput}.ExecuteAsync"/>
    public Task<TimeOutWaitingOutput> ExecuteAsync(TimeOutWaitingInput input, CancellationToken ct) =>
        transactionManager.RunAsync(async token =>
        {
            var now = await clock.GetNowAsync(token);
            var timedOut = await reservations.LockTimedOutWaitingAsync(input.BatchSize, token);

            foreach (var reservation in timedOut)
                await reservations.TimeOutAsync(reservation, now, token);

            return new TimeOutWaitingOutput(timedOut.Count);
        }, ct);
}
