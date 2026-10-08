using System.Globalization;
using System.Net;

namespace NepalHub.Api.Features.Email;

/// <summary>The rendered halves of one template, before an address and a purpose are attached.</summary>
public sealed record EmailDraft(string Subject, string HtmlBody, string TextBody);

/// <summary>
/// The transactional messages this phase sends, as literal templates: no package, no engine,
/// no files to copy beside the binary. A name and a link are the only dynamic parts, every
/// text part is plain by construction and HTML-encoded where the shell renders it — so the
/// name of someone who signed up with markup in it stays text on screen and stays itself in
/// the mail client that shows nothing but the text half.
/// </summary>
public static class EmailTemplates
{
    private const string Brand = "Nepal Hub";

    public static EmailDraft VerifyEmail(string fullName, string link, TimeSpan lifetime)
    {
        var greeting = Greeting(fullName);
        var validity = Describe(lifetime);

        var paragraphs = new[]
        {
            $"Hi {greeting},",
            $"One more step and your {Brand} account is ready: confirm that {Brand} can "
            + "reach you at this address by opening the button below.",
            $"The link works once and stops working after {validity}.",
        };

        var footer =
            $"If you did not create a {Brand} account, you can ignore this email — nothing "
            + "is registered until the link is opened.";

        return new EmailDraft(
            Subject: $"Confirm your email address for {Brand}",
            HtmlBody: Layout("Confirm your email address", paragraphs, "Confirm my email", link, footer),
            TextBody: Text("Confirm your email address", paragraphs, link, footer));
    }

    public static EmailDraft ResetPassword(string fullName, string link, TimeSpan lifetime)
    {
        var greeting = Greeting(fullName);
        var validity = Describe(lifetime);

        var paragraphs = new[]
        {
            $"Hi {greeting},",
            "Someone asked to reset the password for this email's account. Open the button "
            + "below to choose a new one.",
            $"The link stops working after {validity} or as soon as it is used, whichever "
            + "comes first.",
        };

        var footer =
            "If this was not you, no action is needed — your password has not changed, and "
            + "the link above simply expires on its own.";

        return new EmailDraft(
            Subject: $"Reset your password for {Brand}",
            HtmlBody: Layout("Reset your password", paragraphs, "Choose a new password", link, footer),
            TextBody: Text("Reset your password", paragraphs, link, footer));
    }

    public static EmailDraft PasswordChanged(string fullName)
    {
        var greeting = Greeting(fullName);

        var paragraphs = new[]
        {
            $"Hi {greeting},",
            $"The password for your {Brand} account was just changed. Sessions on other "
            + "devices are signed out as the password changes.",
        };

        var footer =
            $"If this was not you, use \u201cForgot password?\u201d on the sign-in page to set "
            + "a new password immediately.";

        return new EmailDraft(
            Subject: $"Your {Brand} password was changed",
            HtmlBody: Layout(
                heading: "Your password was changed",
                paragraphs,
                buttonLabel: null,
                buttonLink: null,
                footer: footer),
            TextBody: Text(
                heading: "Your password was changed",
                paragraphs,
                buttonLink: null,
                footer: footer));
    }

    /// <summary>A name is content, not markup: it travels raw and is encoded only where HTML is built.</summary>
    private static string Greeting(string fullName)
    {
        var trimmed = fullName?.Trim() ?? string.Empty;

        return trimmed.Length == 0 ? "there" : trimmed;
    }

    private static string Describe(TimeSpan lifetime)
    {
        if (lifetime.TotalDays >= 1)
        {
            var days = (int)Math.Round(lifetime.TotalDays);
            return FormattableString.Invariant($"{days} {(days == 1 ? "day" : "days")}");
        }

        if (lifetime.TotalHours >= 1)
        {
            var hours = (int)Math.Round(lifetime.TotalHours);
            return FormattableString.Invariant($"{hours} {(hours == 1 ? "hour" : "hours")}");
        }

        var minutes = Math.Max(1, (int)Math.Round(lifetime.TotalMinutes));
        return FormattableString.Invariant($"{minutes} {(minutes == 1 ? "minute" : "minutes")}");
    }

    /// <summary>
    /// The shared shell, in plain text: heading, dashed rule, paragraphs, the link on a line
    /// of its own so it can be copied out of a client that renders nothing, then the footer.
    /// </summary>
    private static string Text(
        string heading,
        IReadOnlyList<string> paragraphs,
        string? buttonLink,
        string footer)
    {
        var lines = new List<string> { heading, new string('-', heading.Length) };
        lines.AddRange(paragraphs);

        if (buttonLink is not null)
        {
            lines.Add(string.Empty);
            lines.Add(buttonLink);
        }

        lines.Add(string.Empty);
        lines.Add(footer);

        return string.Join("\n", lines);
    }

    /// <summary>
    /// The shared HTML shell. Deliberately plain, table-less, inline-styled markup: that is
    /// what renders consistently across the mail clients people actually read, and every text
    /// part is HTML-encoded here — at the boundary — so no template has to remember to.
    /// </summary>
    private static string Layout(
        string heading,
        IReadOnlyList<string> paragraphs,
        string? buttonLabel,
        string? buttonLink,
        string footer)
    {
        var parts = new List<string>
        {
            $"<h1 style=\"margin:0 0 20px;font-size:22px;line-height:1.3;color:#001c58;\">{Encode(heading)}</h1>",
        };

        parts.AddRange(paragraphs.Select(paragraph =>
            $"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.6;color:#24292f;\">{Encode(paragraph)}</p>"));

        if (buttonLabel is not null && buttonLink is not null)
        {
            parts.Add(
                "<p style=\"margin:24px 0;\">"
                + $"<a href=\"{Encode(buttonLink)}\" style=\"display:inline-block;padding:12px 24px;"
                + "background:#c01033;color:#ffffff;text-decoration:none;font-weight:bold;"
                + $"border-radius:6px;\">{Encode(buttonLabel)}</a></p>");
        }

        parts.Add(
            $"<p style=\"margin:24px 0 0;padding-top:16px;border-top:1px solid #e2e9f6;"
            + $"font-size:13px;line-height:1.6;color:#57606a;\">{Encode(footer)}</p>");

        var body = string.Concat(parts);

        return string.Concat(
            "<!doctype html>\n",
            "<html lang=\"en\">\n",
            "<head>\n",
            "  <meta charset=\"utf-8\">\n",
            "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n",
            "</head>\n",
            "<body style=\"margin:0;padding:24px;background:#f2f5fb;",
            "font-family:Arial,Helvetica,sans-serif;\">\n",
            "  <div style=\"max-width:560px;margin:0 auto;background:#ffffff;",
            "border:1px solid #e2e9f6;border-radius:8px;padding:32px;\">\n",
            body,
            "\n  </div>\n",
            "</body>\n",
            "</html>\n");
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
