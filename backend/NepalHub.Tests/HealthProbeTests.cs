using System.Net;

namespace NepalHub.Tests;

/// <summary>
/// The ops probe: <c>/health</c> answers for the one dependency the whole site has, so a load
/// balancer or a supervisor can pull an instance whose database has gone away.
/// </summary>
public class HealthProbeTests : IClassFixture<NepalHubApiFactory>
{
    private readonly NepalHubApiFactory _factory;

    public HealthProbeTests(NepalHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_probe_reports_healthy_when_the_database_answers()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
