using System.ComponentModel.DataAnnotations;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>Broker settings (section <c>RabbitMq</c>). The URI carries credentials, so it only comes from env/secrets.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public Uri? Uri { get; set; }

    [Required]
    public string ClientName { get; set; } = "identity-service";

    /// <summary>Topic exchange shared by the platform (docs/architecture/04-mensageria.md).</summary>
    [Required]
    public string Exchange { get; set; } = "fiapx.events";

    [Required]
    public string DeadLetterExchange { get; set; } = "fiapx.dlx";
}

/// <summary>Outbox relay settings (section <c>Outbox</c>).</summary>
public sealed class OutboxRelayOptions
{
    public const string SectionName = "Outbox";

    /// <summary>When false the relay is registered but not started (tests drive it by hand).</summary>
    public bool Enabled { get; set; } = true;

    [Range(typeof(TimeSpan), "00:00:00.010", "00:01:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;
}
