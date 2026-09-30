using System.Net;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

public sealed class RateLimitingTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public RateLimitingTests(RateLimitedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Auth_endpoints_reject_once_the_window_is_exhausted()
    {
        using var client = _factory.CreateClient();
        var api = new AuthApiClient(client);

        for (var attempt = 0; attempt < RateLimitedApiFactory.PermitLimit; attempt++)
        {
            using var allowed = await api.LoginAsync(TestData.UniqueEmail(), "Wr0ngPassword!");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var limited = await api.PostForSnapshotAsync(
            "/api/auth/login",
            new { email = TestData.UniqueEmail(), password = "Wr0ngPassword!", rememberMe = false });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);
    }

    [Fact]
    public async Task Read_only_endpoints_are_not_rate_limited()
    {
        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < RateLimitedApiFactory.PermitLimit + 3; attempt++)
        {
            using var response = await client.GetAsync("/api/auth/session");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
