using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NepalHub.Tests;

/// <summary>
/// The single-host delivery path: a built SPA sitting next to the API is served from the same
/// origin as the API, an unknown API path still answers with the JSON envelope rather than a page,
/// and the health endpoint is untouched by either.
/// </summary>
public class SpaServingTests : IClassFixture<SpaServingTests.SpaApiFactory>
{
    private readonly SpaApiFactory _factory;

    public SpaServingTests(SpaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_root_serves_the_built_spa_from_the_same_origin()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Nepal Hub", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_asset_the_spa_shipped_is_served()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/assets/app-a1b2c3.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("console.log", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// The reason the API needs a route of its own before the SPA fallback exists: a page answered
    /// with a 200 is the one response no client can turn into its error envelope, so a typo'd
    /// endpoint has to keep failing the way the rest of the API fails.
    /// </summary>
    [Fact]
    public async Task An_unknown_api_path_stays_a_json_404_rather_than_the_page()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/no-such-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.Equal("not_found", envelope.Error?.Code);
    }

    [Fact]
    public async Task The_health_endpoint_still_reports_the_database()
    {
        using var client = _factory.CreateClient();

        var envelope = await client.GetFromJsonAsync<ApiEnvelope<HealthResponse>>("/api/health");

        Assert.Equal("healthy", envelope?.Data?.Status);
        Assert.Equal("connected", envelope?.Data?.Database);
    }

    /// <summary>
    /// A host that has a built SPA beside it: the fixture writes a minimal dist into a fresh
    /// temporary directory and points <c>Frontend:DistPath</c> at it, so the real host takes the
    /// serving path without the repository gaining a wwwroot folder.
    /// </summary>
    public sealed class SpaApiFactory : NepalHubApiFactory
    {
        private readonly string _spaRoot;

        public SpaApiFactory()
        {
            _spaRoot = Path.Combine(Path.GetTempPath(), $"nepalhub-spa-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(_spaRoot, "assets"));
            File.WriteAllText(
                Path.Combine(_spaRoot, "index.html"),
                "<!doctype html><html><head><title>Nepal Hub</title></head><body>Nepal Hub</body></html>");
            File.WriteAllText(
                Path.Combine(_spaRoot, "assets", "app-a1b2c3.js"),
                "console.log('nepal-hub');");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Frontend:DistPath"] = _spaRoot,
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                try
                {
                    Directory.Delete(_spaRoot, recursive: true);
                }
                catch (IOException)
                {
                    // A leaked temp directory must not fail a passing test run.
                }
            }
        }
    }
}
