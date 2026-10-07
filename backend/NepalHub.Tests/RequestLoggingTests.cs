using System.Net;
using NepalHub.Api.Common;

namespace NepalHub.Tests;

/// <summary>
/// The request id a person or a proxy can follow: present on every answer, echoing what the proxy
/// sent when it is safe to send back, and replaced when it is not.
/// </summary>
public class RequestLoggingTests : IClassFixture<NepalHubApiFactory>
{
    private readonly NepalHubApiFactory _factory;

    public RequestLoggingTests(NepalHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Every_answer_carries_a_request_id_header()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var ids));
        Assert.False(string.IsNullOrWhiteSpace(ids!.Single()));
    }

    [Fact]
    public async Task A_safe_request_id_from_the_proxy_is_echoed_back()
    {
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Request-Id", "edge-42.7");
        using var response = await client.SendAsync(request);

        Assert.Equal("edge-42.7", Assert.Single(response.Headers.GetValues("X-Request-Id")));
    }

    [Fact]
    public async Task An_unsafe_request_id_is_replaced_rather_than_reflected()
    {
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Request-Id", "not safe: id");
        using var response = await client.SendAsync(request);

        var echoed = Assert.Single(response.Headers.GetValues("X-Request-Id"));
        Assert.NotEqual("not safe: id", echoed);
        Assert.DoesNotContain(" ", echoed);
    }

    /// <summary>
    /// The reflection rule in one place: a request id may only be letters, digits, hyphens and
    /// dots, so a forged header carrying a header break or an oversized value can never reach the
    /// response. Everything else falls back to the platform's own id.
    /// </summary>
    [Theory]
    [InlineData("edge-42.7", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData("not safe: id", false)]
    [InlineData("bad\r\nSet-Cookie: injected=1", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public void A_request_id_is_only_accepted_in_a_safe_shape(string value, bool expected) =>
        Assert.Equal(expected, RequestLoggingMiddleware.IsSafeRequestId(value));
}
