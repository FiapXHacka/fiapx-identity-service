using System.Diagnostics;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>A serialized CloudEvent ready to go to the broker.</summary>
public sealed record OutgoingMessage(string MessageId, string Type, ReadOnlyMemory<byte> Body, DateTimeOffset OccurredAt)
{
    /// <summary>The routing key is the CloudEvent type (docs/architecture/04-mensageria.md).</summary>
    public string RoutingKey => Type;
}

public interface IMessagePublisher
{
    /// <summary>Completes only after the broker confirmed the message (publisher confirms); throws otherwise.</summary>
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Publishes persistent messages to the <c>fiapx.events</c> topic exchange with publisher confirms.
/// The exchanges are declared (idempotently) every time a channel is opened, so the service never
/// depends on someone having created the topology before it.
/// </summary>
public sealed class RabbitMqPublisher(RabbitMqConnection connection, IOptions<RabbitMqOptions> options) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;

        using var activity = MessagingTelemetry.Source.StartActivity($"{message.Type} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", settings.ExchangeName);
        activity?.SetTag("messaging.message.id", message.MessageId);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var channel = await GetChannelAsync(settings, cancellationToken).ConfigureAwait(false);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = CloudEventJson.ContentType,
                MessageId = message.MessageId,
                Type = message.Type,
                Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            };

            // With publisher confirmation tracking enabled this awaits the broker's ack and throws on nack.
            await channel.BasicPublishAsync(settings.ExchangeName, message.RoutingKey, mandatory: false,
                properties, message.Body, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await ResetChannelAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await ResetChannelAsync().ConfigureAwait(false);
        var conn = await connection.GetAsync(cancellationToken).ConfigureAwait(false);
        var channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken).ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(settings.ExchangeName, ExchangeType.Topic, durable: true,
            autoDelete: false, arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.ExchangeDeclareAsync(settings.DeadLetterExchangeName, ExchangeType.Direct, durable: true,
            autoDelete: false, arguments: null, cancellationToken: cancellationToken).ConfigureAwait(false);

        _channel = channel;
        return channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync().ConfigureAwait(false);
            _channel = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetChannelAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
