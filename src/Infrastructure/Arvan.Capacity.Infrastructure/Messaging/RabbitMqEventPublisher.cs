using System.Text.Json.Nodes;
using Arvan.Capacity.Application.Clients;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Application.Telemetry;
using Arvan.Capacity.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Arvan.Capacity.Infrastructure.Messaging;

/// <inheritdoc cref="IEventPublisher"/>
/// <remarks>
/// Publishes to the durable topic exchange <c>capacity.events</c>, each message persistent with
/// <c>message-id</c> set to the event id, and waits for the broker's confirm of every message. A
/// failure drops the connection so the next batch reconnects, and propagates so the relay's
/// transaction rolls back and the batch is sent again.
/// </remarks>
internal sealed class RabbitMqEventPublisher(IOptions<RabbitMqOptions> rabbit, IOptions<CapacityOptions> capacity)
    : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    /// <inheritdoc cref="IEventPublisher.PublishAsync"/>
    public async Task PublishAsync(IReadOnlyList<OutboxEvent> events, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var channel = await ChannelAsync(ct);

            foreach (var e in events)
            {
                var eventId = $"{capacity.Value.Cell}:{e.Id}";
                var envelope = JsonNode.Parse(e.Payload)!.AsObject();
                envelope["event_id"] = eventId;

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = eventId,
                };

                await channel.BasicPublishAsync(
                    rabbit.Value.Exchange, e.RoutingKey, mandatory: false, properties,
                    System.Text.Encoding.UTF8.GetBytes(envelope.ToJsonString()), ct);
            }
        }
        catch
        {
            CapacityTelemetry.RelayPublishFailures.Add(1);
            await DisposeConnectionAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync() => await DisposeConnectionAsync();

    private async Task<IChannel> ChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await DisposeConnectionAsync();

        _connection = await new ConnectionFactory { Uri = new Uri(rabbit.Value.Uri!) }.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        await _channel.ExchangeDeclareAsync(rabbit.Value.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        return _channel;
    }

    private async Task DisposeConnectionAsync()
    {
        try
        {
            if (_channel is not null) await _channel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
        }
        catch (Exception)
        {
            // The connection is already broken; the next publish builds a new one.
        }

        _channel = null;
        _connection = null;
    }
}
