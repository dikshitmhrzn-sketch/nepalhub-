namespace NepalHub.Api.Features.Email;

/// <summary>
/// Builds the one URL an email ever carries. Both halves are configuration-derived: the
/// origin comes from <c>Email:WebBaseUrl</c> and never from a request header, so a link in
/// an email can only ever point at this deployment, and the token is percent-encoded on the
/// way in.
///
/// <para>
/// The encoding is not decoration. Identity tokens are standard base64, which contains
/// <c>+</c>, <c>/</c> and <c>=</c>; in a query string a bare <c>+</c> reads back as a space
/// and every token would then fail to verify. <see cref="Uri.EscapeDataString"/> writes
/// <c>%2B</c> and friends, which a browser hands back intact through
/// <c>URLSearchParams</c> and a pasted link survives on the way to the API.
/// </para>
/// </summary>
public sealed class EmailLinkBuilder(EmailSettings settings)
{
    public string BuildVerifyEmailLink(string email, string token) =>
        Build("verify-email", email, token);

    public string BuildResetPasswordLink(string email, string token) =>
        Build("reset-password", email, token);

    private string Build(string route, string email, string token)
    {
        if (!Uri.TryCreate(settings.WebBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "Email:WebBaseUrl must be the absolute origin of the site before any email "
                + "link can be built.");
        }

        var origin = settings.WebBaseUrl.TrimEnd('/');

        return FormattableString.Invariant(
            $"{origin}/{route}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");
    }
}
