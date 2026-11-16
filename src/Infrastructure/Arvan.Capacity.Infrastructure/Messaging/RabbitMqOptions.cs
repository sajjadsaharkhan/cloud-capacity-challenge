namespace Arvan.Capacity.Infrastructure.Messaging;

/// <summary>
/// Settings under <c>RabbitMq</c> in configuration.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// The broker's AMQP URI. Empty disables publishing: the relay logs events instead.
    /// </summary>
    public string? Uri { get; init; }

    /// <summary>
    /// The topic exchange events are published to.
    /// </summary>
    public string Exchange { get; init; } = "capacity.events";
}
