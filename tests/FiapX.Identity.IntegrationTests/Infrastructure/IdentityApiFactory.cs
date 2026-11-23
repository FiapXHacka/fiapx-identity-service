using FiapX.Identity.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FiapX.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// The real API wired to the containers. The background relay is off so tests drive
/// <see cref="OutboxRelay.PublishPendingAsync"/> deterministically.
/// </summary>
public sealed class IdentityApiFactory(PlatformFixture platform, Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", platform.PostgresConnectionString);
        builder.UseSetting("RabbitMq:Uri", platform.RabbitMqUri.ToString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Outbox:Enabled", "false");
        if (configureServices is not null)
        {
            builder.ConfigureServices(configureServices);
        }
    }
}
