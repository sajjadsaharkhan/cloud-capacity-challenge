using Arvan.Capacity.Application.Clients;
using Arvan.Capacity.Application.Repositories;
using Microsoft.Extensions.Logging;

namespace Arvan.Capacity.Infrastructure.Messaging;

/// <inheritdoc cref="IEventPublisher"/>
/// <remarks>
/// Used when no broker is configured: the relay still drains the outbox, and the events are logged
/// instead of published.
/// </remarks>
internal sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : IEventPublisher
{
    /// <inheritdoc cref="IEventPublisher.PublishAsync"/>
    public Task PublishAsync(IReadOnlyList<OutboxEvent> events, CancellationToken ct)
    {
        foreach (var e in events)
            logger.LogInformation("Event {EventId} {RoutingKey}: {Payload}", e.Id, e.RoutingKey, e.Payload);

        return Task.CompletedTask;
    }
}
