using FiapX.Identity.Application.Abstractions;
using FiapX.Identity.Infrastructure.Messaging;
using FiapX.Identity.Infrastructure.Persistence;
using FiapX.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FiapX.Identity.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";
    private const string EventSource = "fiapx/identity-service";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
        services.AddDbContext<IdentityDbContext>(options => IdentityDbContext.Configure(options, connectionString));

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<OutboxRelayOptions>()
            .Bind(configuration.GetSection(OutboxRelayOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp => new CloudEventFactory(sp.GetRequiredService<TimeProvider>(), EventSource));
        services.AddScoped<IEventOutbox, EventOutbox>();

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.AddSingleton<OutboxRelay>();
        services.AddHostedService<OutboxRelayHost>();

        services.AddHealthChecks()
            .AddDbContextCheck<IdentityDbContext>("postgres", tags: [ReadyTag])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [ReadyTag]);

        return services;
    }

    /// <summary>Starts the relay only when <c>Outbox:Enabled</c> is true.</summary>
    private sealed class OutboxRelayHost(OutboxRelay relay, IOptions<OutboxRelayOptions> options) : Microsoft.Extensions.Hosting.IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            options.Value.Enabled ? relay.StartAsync(cancellationToken) : Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) =>
            options.Value.Enabled ? relay.StopAsync(cancellationToken) : Task.CompletedTask;
    }

    /// <summary>Applies pending EF Core migrations (local/compose and tests; production runs them in the pipeline).</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
