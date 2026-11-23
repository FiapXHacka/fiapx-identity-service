using System.Text.Json;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>An event waiting to be published, stored in the same transaction as the change that produced it.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public int Attempts { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage From<TData>(CloudEvent<TData> cloudEvent)
    {
        ArgumentNullException.ThrowIfNull(cloudEvent);
        return new OutboxMessage
        {
            Id = Guid.Parse(cloudEvent.Id),
            Type = cloudEvent.Type,
            Payload = JsonSerializer.Serialize(cloudEvent, CloudEventJson.Options),
            OccurredAt = cloudEvent.Time,
        };
    }

    public void MarkPublished(DateTimeOffset publishedAt) => PublishedAt = publishedAt;

    public void RegisterFailedAttempt() => Attempts++;
}
