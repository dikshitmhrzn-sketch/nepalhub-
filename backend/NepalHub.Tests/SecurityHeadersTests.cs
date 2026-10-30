using System.Net;

namespace NepalHub.Tests;

/// <summary>
/// The safety rail leaves with every response regardless of who asked for it or what the
/// answer was, so both a live endpoint and the SPA root are probed from outside the host.
/// The eSewa allowance in the content policy is asserted rather than assumed: a checkout
/// that silently fails to leave for the payment provider is the one regression this header
/// could plausibly cause, and it would not be visible in any other test.
/// </summary>
public sealed class SecurityHeadersTests(NepalHubApiFactory factory)
    : IClassFixture<NepalHubApiFactory>
{
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/")]
    public async Task Every_response_carries_the_security_headers(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Contains("nosniff", HeaderValues(response, "X-Content-Type-Options"));
        Assert.Contains("DENY", HeaderValues(response, "X-Frame-Options"));
        Assert.Contains("strict-origin-when-cross-origin", HeaderValues(response, "Referrer-Policy"));

        var contentPolicy = string.Join(" ", HeaderValues(response, "Content-Security-Policy"));
        Assert.Contains("frame-ancestors 'none'", contentPolicy);
        Assert.Contains("script-src 'self'", contentPolicy);
        Assert.Contains("object-src 'none'", contentPolicy);
        Assert.Contains("form-action 'self' https://*.esewa.com.np", contentPolicy);
    }

    private static string[] HeaderValues(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? values.ToArray()
            : [];
}
