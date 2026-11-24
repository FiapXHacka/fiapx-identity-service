using FiapX.Identity.Infrastructure.Messaging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FiapX.Identity.Api;

/// <summary>OpenTelemetry traces and metrics (ADR-0015). Exported via OTLP only when an endpoint is configured.</summary>
public static class Observability
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, string defaultServiceName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? defaultServiceName;

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal))
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource(MessagingTelemetry.SourceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return services;
    }
}
