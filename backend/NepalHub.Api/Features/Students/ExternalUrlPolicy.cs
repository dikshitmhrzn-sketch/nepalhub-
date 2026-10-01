using System.Net;

namespace NepalHub.Api.Features.Students;

/// <summary>
/// Decides whether a link a staff member typed is safe to store and hand back to a student.
///
/// Three things are being defended, and none of them is the server fetching the URL:
///
/// <para>
/// <b>Scheme.</b> Only https is accepted. This is the check that matters most, and it is
/// absolute rather than a blocklist: an allowlist of one scheme cannot be defeated by the
/// javascript:, data:, vbscript: and file: variants, or by a scheme nobody thought of yet. A
/// staff account is trusted but not infallible, and a stored javascript: link is a stored
/// cross-site scripting bug that fires in the browser of every student who clicks it.
/// </para>
///
/// <para>
/// <b>Host.</b> The host must contain a dot, so <c>https://localhost/</c> is refused, and it must
/// not be an IP literal, so <c>https://169.254.169.254/</c> and <c>https://127.0.0.1/</c> are
/// refused. Storing such a link is not a server-side request forgery risk, because this service
/// never fetches anything — the risk is a student being invited in a browser to open an internal
/// or metadata address, which is a real phishing and local-network attack technique.
/// </para>
///
/// <para>
/// <b>Shape.</b> Absolute, within the length cap, and with a scheme and host that actually
/// parsed rather than a relative path or a bare string.
/// </para>
///
/// The normalised result is the canonical form <see cref="Uri"/> produces rather than the raw
/// text, so what is validated is exactly what is stored and later rendered into an href.
/// </summary>
internal static class ExternalUrlPolicy
{
    internal const string Field = "externalUrl";

    internal const int MaxLength = 500;

    /// <summary>
    /// Validates and canonicalises an optional link. An absent or whitespace-only value is
    /// accepted and normalised to null, because "no link" is a legitimate answer for a note and
    /// for a scholarship, and is checked separately for a question paper.
    /// </summary>
    internal static bool TryNormalise(string? raw, out string? normalised, out string? error) =>
        TryNormalise(raw, Field, out normalised, out error);

    /// <summary>
    /// The same check, reporting failures against a caller-supplied field name. The job module
    /// reuses this policy for its application link and needs its own field name, so the error a
    /// staff member sees points at the input they actually filled in. The validation itself is
    /// deliberately identical: a javascript: link is equally unsafe in either module, and two
    /// copies of this rule would eventually disagree about what is safe.
    /// </summary>
    internal static bool TryNormalise(string? raw, string field, out string? normalised, out string? error)
    {
        normalised = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        if (trimmed.Length > MaxLength)
        {
            error = $"The link must be {MaxLength} characters or fewer.";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "Enter a full link starting with https://";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Only https links are allowed, because that is the only kind we can vouch for.";
            return false;
        }

        var host = uri.DnsSafeHost;

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "That link has no host name, so it cannot be opened.";
            return false;
        }

        if (!host.Contains('.', StringComparison.Ordinal))
        {
            error = "Use a full host name such as example.gov.np, not a single word like localhost.";
            return false;
        }

        if (IPAddress.TryParse(host, out _))
        {
            error = "Use a web address rather than a bare IP number.";
            return false;
        }

        normalised = uri.AbsoluteUri;
        return true;
    }
}
