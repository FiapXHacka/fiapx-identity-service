using System.Diagnostics;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>Builds CloudEvents with this service's source, a time-ordered id and the current trace context.</summary>
public sealed class CloudEventFactory(TimeProvider timeProvider, string source)
{
    public string Source { get; } = string.IsNullOrWhiteSpace(source)
        ? throw new ArgumentException("Source is required.", nameof(source))
        : source;

    public CloudEvent<TData> Create<TData>(string type, string subject, TData data, int dataSchemaVersion = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentOutOfRangeException.ThrowIfLessThan(dataSchemaVersion, 1);

        var now = timeProvider.GetUtcNow();
        return new CloudEvent<TData>(
            SpecVersion: CloudEventJson.SpecVersion,
            Id: Guid.CreateVersion7(now).ToString(),
            Source: Source,
            Type: type,
            Time: now,
            Subject: subject,
            DataSchema: $"fiapx/{type}/v{dataSchemaVersion}",
            DataContentType: CloudEventJson.DataContentType,
            TraceParent: CurrentTraceParent(),
            Data: data);
    }

    // W3C trace context of the request that produced the event, so the trace continues in the consumer (ADR-0015).
    private static string? CurrentTraceParent() =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.Id : null;
}
