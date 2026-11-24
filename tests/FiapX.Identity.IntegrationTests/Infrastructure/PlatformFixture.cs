using FiapX.Identity.IntegrationTests.Infrastructure;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(PlatformFixture))]

namespace FiapX.Identity.IntegrationTests.Infrastructure;

/// <summary>Real Postgres and RabbitMQ, started once for the whole test assembly.</summary>
public sealed class PlatformFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17.12-alpine")
        .WithDatabase("identity_db")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.2.9-management")
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public Uri RabbitMqUri => new(_rabbitMq.GetConnectionString());

    public async ValueTask InitializeAsync() =>
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
