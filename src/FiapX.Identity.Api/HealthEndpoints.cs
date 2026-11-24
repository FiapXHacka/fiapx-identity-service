using System.Text.Json;
using FiapX.Identity.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FiapX.Identity.Api;

/// <summary>Kubernetes probes: liveness never touches dependencies, readiness checks Postgres and RabbitMQ.</summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag),
            ResponseWriter = WriteResponseAsync,
        });
        return endpoints;
    }

    internal static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new { status = entry.Value.Status.ToString(), description = entry.Value.Description }),
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, body, JsonSerializerOptions.Web, context.RequestAborted);
    }
}
