using System.Net;

namespace NepalHub.Tests;

/// <summary>
/// The proxy-facing half of transport hardening: a scheme and address delivered by a proxy the app
/// trusts has to reach HSTS and the cookie flags, while a plain request with no proxy speaking for
/// it keeps the localhost development behaviour it has today.
/// </summary>
public class ForwardedHeadersTests : IClassFixture<NepalHubApiFactory>
{
    private readonly NepalHubApiFactory _factory;

    public ForwardedHeadersTests(NepalHubApiFactory factory) => _factory = factory;

    /// <summary>
    /// Why SameAsRequest plus X-Forwarded-Proto is the production rule: behind the TLS proxy the
    /// request arrives as https, so the cookies come back marked Secure and the response carries
    /// the HSTS header a browser needs before it will stop offering plain http to this host. The
    /// Host header has to be a real production name because HSTS refuses to answer for localhost,
    /// which its own default excluded list keeps safe from a poisoned transport policy.
    /// </summary>
    [Fact]
    public async Task A_forwarded_https_request_gets_hsts_and_a_secure_cookie()
    {
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        request.Headers.Host = "nepalhub.example.com";
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Strict-Transport-Security", response.Headers.Select(header => header.Key));

        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.NotEmpty(cookies);
        Assert.All(cookies, cookie => Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Development and the emulator reach the API over plain http where no proxy speaks for them,
    /// and that request must not be told to refuse itself: no HSTS header, and no Secure flag on a
    /// cookie that plain http would then drop on the floor.
    /// </summary>
    [Fact]
    public async Task A_plain_http_request_keeps_no_hsts_and_no_secure_cookie()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Strict-Transport-Security", response.Headers.Select(header => header.Key));

        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.NotEmpty(cookies);
        Assert.All(cookies, cookie => Assert.DoesNotContain("; secure", cookie, StringComparison.OrdinalIgnoreCase));
    }
}
