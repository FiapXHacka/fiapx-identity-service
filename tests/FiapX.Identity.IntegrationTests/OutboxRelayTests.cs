using System.Text;
using System.Text.Json;
using FiapX.Identity.Application.Abstractions;
using FiapX.Identity.Application.IntegrationEvents;
using FiapX.Identity.Domain.Users;
using FiapX.Identity.Infrastructure.Messaging;
using FiapX.Identity.Infrastructure.Persistence;
using FiapX.Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client;

namespace FiapX.Identity.IntegrationTests;

public sealed class OutboxRelayTests(PlatformFixture platform) : IAsyncLifetime
{
    private IdentityApiFactory _factory = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new IdentityApiFactory(platform);
        _ = _factory.Server; // boots the host and applies the migrations
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(Ct);
        await db.Users.ExecuteDeleteAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private async Task<Guid> RegisterUserWithEventAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IEventOutbox>();
        var user = User.Register(Guid.CreateVersion7(), email, "Ana", "hash", DateTimeOffset.UtcNow);

        db.Users.Add(user);
        outbox.Enqueue(UserRegistered.EventType, user.Id.ToString(), new UserRegistered(user.Id, user.Email, user.Name));
        await db.SaveChangesAsync(Ct); // business change and event commit together

        return user.Id;
    }

    private async Task<(IConnection Connection, IChannel Channel, string Queue)> BindTestQueueAsync()
    {
        var connection = await new ConnectionFactory { Uri = platform.RabbitMqUri }.CreateConnectionAsync(Ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await channel.ExchangeDeclareAsync("fiapx.events", ExchangeType.Topic, durable: true, cancellationToken: Ct);
        var queue = (await channel.QueueDeclareAsync(cancellationToken: Ct)).QueueName;
        await channel.QueueBindAsync(queue, "fiapx.events", "user.registered", cancellationToken: Ct);
        return (connection, channel, queue);
    }

    private static async Task<BasicGetResult> WaitForMessageAsync(IChannel channel, string queue, CancellationToken ct)
    {
        for (var i = 0; i < 50; i++)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: true, ct);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(100, ct);
        }

        throw new TimeoutException("No message arrived on the test queue.");
    }

    [Fact]
    public async Task Publishes_pending_events_with_confirms_and_marks_them_published()
    {
        var (connection, channel, queue) = await BindTestQueueAsync();
        await using var _ = connection;
        await using var __ = channel;
        var userId = await RegisterUserWithEventAsync("ana@example.com");
        var relay = _factory.Services.GetRequiredService<OutboxRelay>();

        var result = await relay.PublishPendingAsync(Ct);

        result.ShouldBe(new OutboxBatchResult(Published: 1, Failed: false));
        var delivered = await WaitForMessageAsync(channel, queue, Ct);
        delivered.BasicProperties.ContentType.ShouldBe(CloudEventJson.ContentType);
        delivered.BasicProperties.Persistent.ShouldBeTrue();
        delivered.BasicProperties.Type.ShouldBe("user.registered");
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(delivered.Body.Span));
        json.RootElement.GetProperty("data").GetProperty("userId").GetGuid().ShouldBe(userId);
        json.RootElement.GetProperty("id").GetString().ShouldBe(delivered.BasicProperties.MessageId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.OutboxMessages.SingleAsync(Ct)).PublishedAt.ShouldNotBeNull();
        (await relay.PublishPendingAsync(Ct)).Published.ShouldBe(0, "already published messages are not sent again");
    }

    [Fact]
    public async Task Broker_failure_keeps_the_message_pending_and_counts_the_attempt()
    {
        await RegisterUserWithEventAsync("bruno@example.com");
        var failingPublisher = Substitute.For<IMessagePublisher>();
        failingPublisher.PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker down"));
        var relay = new OutboxRelay(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            failingPublisher,
            TimeProvider.System,
            _factory.Services.GetRequiredService<IOptions<OutboxRelayOptions>>(),
            _factory.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OutboxRelay>>());

        var result = await relay.PublishPendingAsync(Ct);

        result.ShouldBe(new OutboxBatchResult(Published: 0, Failed: true));
        await using var scope = _factory.Services.CreateAsyncScope();
        var message = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OutboxMessages.SingleAsync(Ct);
        message.PublishedAt.ShouldBeNull();
        message.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Background_relay_publishes_on_its_own()
    {
        var (connection, channel, queue) = await BindTestQueueAsync();
        await using var _ = connection;
        await using var __ = channel;
        await RegisterUserWithEventAsync("carla@example.com");
        var relay = _factory.Services.GetRequiredService<OutboxRelay>();

        await relay.StartAsync(Ct);
        try
        {
            var delivered = await WaitForMessageAsync(channel, queue, Ct);
            delivered.BasicProperties.Type.ShouldBe("user.registered");
        }
        finally
        {
            await relay.StopAsync(Ct);
        }
    }

    [Fact]
    public async Task Email_is_unique_regardless_of_case()
    {
        await RegisterUserWithEventAsync("dani@example.com");

        await Should.ThrowAsync<DbUpdateException>(() => RegisterUserWithEventAsync("DANI@example.com"));
    }

    [Fact]
    public async Task Database_rejects_emails_without_at_sign()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var insert = () => db.Database.ExecuteSqlAsync(
            $"INSERT INTO users (id, email, name, password_hash) VALUES ({Guid.CreateVersion7()}, 'no-at-sign', 'x', 'h')", Ct);

        await Should.ThrowAsync<Npgsql.PostgresException>(insert);
    }
}
