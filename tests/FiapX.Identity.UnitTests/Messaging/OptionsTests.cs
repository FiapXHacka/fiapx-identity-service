using System.ComponentModel.DataAnnotations;
using FiapX.Identity.Infrastructure.Messaging;
using FiapX.Identity.Infrastructure.Security;

namespace FiapX.Identity.UnitTests.Messaging;

public sealed class OptionsTests
{
    private static List<ValidationResult> Validate(object options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void RabbitMq_defaults_follow_the_platform_topology()
    {
        var options = new RabbitMqOptions();

        options.Exchange.ShouldBe("fiapx.events");
        options.DeadLetterExchange.ShouldBe("fiapx.dlx");
        options.ClientName.ShouldBe("identity-service");
    }

    [Fact]
    public void RabbitMq_uri_is_required()
    {
        Validate(new RabbitMqOptions()).ShouldContain(r => r.MemberNames.Contains(nameof(RabbitMqOptions.Uri)));
        Validate(new RabbitMqOptions { Uri = new Uri("amqp://localhost") }).ShouldBeEmpty();
    }

    [Fact]
    public void Outbox_defaults_are_valid()
    {
        var options = new OutboxRelayOptions();

        Validate(options).ShouldBeEmpty();
        options.Enabled.ShouldBeTrue();
        options.BatchSize.ShouldBe(50);
        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(1));
        options.MaxBackoff.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Outbox_rejects_out_of_range_values() =>
        Validate(new OutboxRelayOptions { BatchSize = 0, PollInterval = TimeSpan.Zero, MaxBackoff = TimeSpan.FromHours(1) })
            .Count.ShouldBe(3);

    [Fact]
    public void Auth_defaults_to_the_platform_audience_and_no_issuer()
    {
        var options = new AuthOptions();

        options.Audience.ShouldBe("fiapx-api");
        options.Issuer.ShouldBeNull();
        Validate(options).ShouldBeEmpty();
    }

    [Fact]
    public void Auth_accepts_an_absolute_issuer() =>
        Validate(new AuthOptions { Issuer = new Uri("http://localhost:8080") }).ShouldBeEmpty();

    [Fact]
    public void Auth_rejects_a_relative_issuer() =>
        Validate(new AuthOptions { Issuer = new Uri("/relative", UriKind.Relative) })
            .ShouldContain(r => r.MemberNames.Contains(nameof(AuthOptions.Issuer)));

    [Fact]
    public void Auth_requires_an_audience() =>
        Validate(new AuthOptions { Audience = "" })
            .ShouldContain(r => r.MemberNames.Contains(nameof(AuthOptions.Audience)));
}
