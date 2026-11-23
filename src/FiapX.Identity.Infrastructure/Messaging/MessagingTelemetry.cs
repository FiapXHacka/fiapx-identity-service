using System.Diagnostics;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>Activity source for broker operations; registered in the OpenTelemetry tracer (ADR-0015).</summary>
public static class MessagingTelemetry
{
    public const string SourceName = "FiapX.Messaging";

    public static ActivitySource Source { get; } = new(SourceName);
}
