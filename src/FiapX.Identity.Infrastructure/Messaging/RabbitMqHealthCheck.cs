using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>Readiness: the broker connection can be opened (the outbox still accepts writes when it cannot).</summary>
internal sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var conn = await connection.GetAsync(cancellationToken).ConfigureAwait(false);
            return conn.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Connection is closed.");
        }
#pragma warning disable CA1031 // Any failure means "not ready".
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("Cannot connect to RabbitMQ.", ex);
        }
    }
}
