using System.Net;
using System.Text.Json;
using FiapX.Identity.IntegrationTests.Infrastructure;

namespace FiapX.Identity.IntegrationTests;

public sealed class HealthTests(PlatformFixture platform)
{
    [Fact]
    public async Task Live_does_not_depend_on_anything()
    {
        await using var factory = new IdentityApiFactory(platform);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_checks_postgres_and_rabbitmq()
    {
        await using var factory = new IdentityApiFactory(platform);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var checks = json.RootElement.GetProperty("checks");
        checks.GetProperty("postgres").GetProperty("status").GetString().ShouldBe("Healthy");
        checks.GetProperty("rabbitmq").GetProperty("status").GetString().ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_reports_unavailable_broker()
    {
        await using var factory = new IdentityApiFactory(platform).WithWebHostBuilder(builder =>
            builder.UseSetting("RabbitMq:Uri", "amqp://guest:guest@127.0.0.1:1/"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
