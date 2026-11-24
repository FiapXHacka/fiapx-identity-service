using System.Diagnostics;
using System.Text.Json;
using FiapX.Identity.Application.IntegrationEvents;
using FiapX.Identity.Infrastructure.Messaging;
using Microsoft.Extensions.Time.Testing;

namespace FiapX.Identity.UnitTests.Messaging;

public sealed class CloudEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 18, 4, 5, TimeSpan.Zero);
    private readonly CloudEventFactory _factory = new(new FakeTimeProvider(Now), "fiapx/identity-service");

    [Fact]
    public void Factory_fills_the_envelope()
    {
        var userId = Guid.CreateVersion7(Now);

        var cloudEvent = _factory.Create(UserRegistered.EventType, userId.ToString(),
            new UserRegistered(userId, "ana@example.com", "Ana"));

        cloudEvent.SpecVersion.ShouldBe("1.0");
        cloudEvent.Source.ShouldBe("fiapx/identity-service");
        cloudEvent.Type.ShouldBe("user.registered");
        cloudEvent.Subject.ShouldBe(userId.ToString());
        cloudEvent.Time.ShouldBe(Now);
        cloudEvent.DataSchema.ShouldBe("fiapx/user.registered/v1");
        cloudEvent.DataContentType.ShouldBe("application/json");
        var id = Guid.Parse(cloudEvent.Id);
        id.Version.ShouldBe(7);
    }

    [Fact]
    public void Factory_uses_the_requested_schema_version() =>
        _factory.Create("user.registered", "s", new { }, dataSchemaVersion: 2).DataSchema.ShouldBe("fiapx/user.registered/v2");

    [Fact]
    public void Factory_propagates_the_current_w3c_trace()
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();

        var cloudEvent = _factory.Create("user.registered", "s", new { });

        cloudEvent.TraceParent.ShouldBe(activity.Id);
        cloudEvent.TraceParent!.ShouldStartWith("00-");
    }

    [Fact]
    public void Factory_omits_traceparent_without_an_activity()
    {
        Activity.Current = null;

        _factory.Create("user.registered", "s", new { }).TraceParent.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "subject", 1)]
    [InlineData("user.registered", " ", 1)]
    [InlineData("user.registered", "subject", 0)]
    public void Factory_validates_arguments(string type, string subject, int version) =>
        Should.Throw<ArgumentException>(() => _factory.Create(type, subject, new { }, version));

    [Fact]
    public void Factory_requires_a_source() =>
        Should.Throw<ArgumentException>(() => new CloudEventFactory(TimeProvider.System, " "));

    [Fact]
    public void Serialized_event_matches_the_asyncapi_contract()
    {
        Activity.Current = null;
        var userId = Guid.Parse("01936f50-0000-7000-8000-000000000042");
        var cloudEvent = _factory.Create(UserRegistered.EventType, userId.ToString(),
            new UserRegistered(userId, "ana@example.com", "Ana"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(cloudEvent, CloudEventJson.Options));
        var root = json.RootElement;

        // Required attributes of CloudEventBase in fiapx/docs/contracts/asyncapi.yaml
        foreach (var name in new[] { "specversion", "id", "source", "type", "time", "subject", "dataschema", "data" })
        {
            root.TryGetProperty(name, out _).ShouldBeTrue($"missing '{name}'");
        }

        root.TryGetProperty("traceparent", out _).ShouldBeFalse("null traceparent must be omitted");
        root.GetProperty("datacontenttype").GetString().ShouldBe("application/json");
        var data = root.GetProperty("data");
        data.GetProperty("userId").GetGuid().ShouldBe(userId);
        data.GetProperty("email").GetString().ShouldBe("ana@example.com");
        data.GetProperty("name").GetString().ShouldBe("Ana");
    }

    [Fact]
    public void Contract_example_deserializes_with_a_tolerant_reader()
    {
        const string example = """
            {
              "specversion": "1.0",
              "id": "01936f5e-8a4b-7c3d-9e2f-1a2b3c4d5e6f",
              "source": "fiapx/identity-service",
              "type": "user.registered",
              "time": "2026-11-23T18:04:05Z",
              "subject": "01936f50-0000-7000-8000-000000000042",
              "dataschema": "fiapx/user.registered/v1",
              "datacontenttype": "application/json",
              "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
              "unknownExtension": true,
              "data": { "userId": "01936f50-0000-7000-8000-000000000042", "email": "ana@example.com", "name": "Ana", "extra": 1 }
            }
            """;

        var cloudEvent = JsonSerializer.Deserialize<CloudEvent<UserRegistered>>(example, CloudEventJson.Options)!;

        cloudEvent.Type.ShouldBe("user.registered");
        cloudEvent.TraceParent.ShouldStartWith("00-4bf9");
        cloudEvent.Data.Email.ShouldBe("ana@example.com");
    }

    [Fact]
    public void Outbox_message_is_built_from_the_event()
    {
        var cloudEvent = _factory.Create("user.registered", "s", new { value = 1 });

        var message = OutboxMessage.From(cloudEvent);

        message.Id.ShouldBe(Guid.Parse(cloudEvent.Id));
        message.Type.ShouldBe("user.registered");
        message.OccurredAt.ShouldBe(Now);
        message.PublishedAt.ShouldBeNull();
        message.Attempts.ShouldBe(0);
        JsonDocument.Parse(message.Payload).RootElement.GetProperty("data").GetProperty("value").GetInt32().ShouldBe(1);

        message.RegisterFailedAttempt();
        message.MarkPublished(Now.AddSeconds(1));
        message.Attempts.ShouldBe(1);
        message.PublishedAt.ShouldBe(Now.AddSeconds(1));
    }

    [Fact]
    public void Outbox_message_requires_an_event() =>
        Should.Throw<ArgumentNullException>(() => OutboxMessage.From<object>(null!));

    [Fact]
    public void Outgoing_message_routes_by_type() =>
        new OutgoingMessage("id", "user.registered", ReadOnlyMemory<byte>.Empty, Now).RoutingKey.ShouldBe("user.registered");
}
