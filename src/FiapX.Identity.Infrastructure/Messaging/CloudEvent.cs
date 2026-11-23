using System.Text.Json;
using System.Text.Json.Serialization;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>
/// CloudEvents 1.0 envelope in structured JSON mode (ADR-0006). The contract lives in
/// fiapx/docs/contracts/asyncapi.yaml; property names here must match it.
/// </summary>
public sealed record CloudEvent<TData>(
    [property: JsonPropertyName("specversion")] string SpecVersion,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("time")] DateTimeOffset Time,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("dataschema")] string DataSchema,
    [property: JsonPropertyName("datacontenttype")] string DataContentType,
    [property: JsonPropertyName("traceparent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TraceParent,
    [property: JsonPropertyName("data")] TData Data);

/// <summary>Serialization settings shared by every CloudEvent this service writes or reads.</summary>
public static class CloudEventJson
{
    public const string SpecVersion = "1.0";
    public const string ContentType = "application/cloudevents+json";
    public const string DataContentType = "application/json";

    /// <summary>camelCase for <c>data</c> (e.g. <c>userId</c>), case-insensitive reads (tolerant reader).</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
