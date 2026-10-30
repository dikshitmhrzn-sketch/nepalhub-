namespace NepalHub.Api.Common;

/// <summary>
/// The safety rail every response carries, whatever produced it - an API endpoint, a static
/// asset, a 404, an error. Headers are set before the rest of the pipeline runs, so even a
/// response that fails halfway down still leaves the browser with them.
///
/// <para>
/// <c>nosniff</c> stops a browser from guessing a file's type and executing something the
/// server never meant as code. <c>X-Frame-Options: DENY</c> with <c>frame-ancestors 'none'</c>
/// stops the site being framed inside someone else's page, which is how clickjacking makes a
/// visitor click what they cannot see. The referrer policy keeps this app's URLs out of the
/// addresses of sites users click away to.
/// </para>
///
/// <para>
/// The content policy is the substantial one: scripts and styles load from this origin only,
/// objects are banned outright, and the document's base and form targets cannot be rewritten
/// from elsewhere. Three allowances exist because the application itself needs them -
/// inline style attributes (React writes them), images from any https source (listings and
/// profiles link avatars), and form posts to eSewa, because checkout is a signed form
/// submitted from the buyer's own browser to the payment provider's host. Anything tighter
/// breaks a real feature rather than a hypothetical attack.
/// </para>
///
/// <para>
/// Swagger's own UI is exempt from the policy and only exists in Development: it serves a
/// page full of inline scripts it owns, and a header that silently guts the developer's API
/// explorer teaches everyone to weaken the policy instead of reading it.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    internal const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self' https://*.esewa.com.np; " +
        "frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        // Indexer assignment, not Add: whichever of these the response already carries is
        // replaced rather than doubled, so a second middleware adding its own value later
        // cannot turn one policy into a list the browser reads as two.
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
        }

        await next(context);
    }
}
