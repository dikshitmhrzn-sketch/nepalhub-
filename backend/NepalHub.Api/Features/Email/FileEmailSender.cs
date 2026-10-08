using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NepalHub.Api.Features.Email;

/// <summary>
/// The development sender: each message is written to disk as an HTML file beside its plain
/// text twin, and the path is logged, so a developer can open what the site "sent" in a
/// browser and click the verification or reset link exactly as a user would — the whole flow,
/// without an SMTP account, a network, or a mailbox anyone could accidentally write to.
///
/// <para>
/// The directory defaults to <c>{temp}/nepalhub-emails</c> and can be pointed anywhere with
/// <c>Email:File:Directory</c>. Files are named by time, purpose and a short random suffix:
/// time-sorted for browsing, purpose-labelled so the right one is obvious, and never colliding
/// when two messages land in the same millisecond.
/// </para>
/// </summary>
public sealed class FileEmailSender(EmailSettings settings, ILogger<FileEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var directory = ResolveDirectory();
        Directory.CreateDirectory(directory);

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var baseName = $"{stamp}-{Sanitize(message.Purpose)}-{Guid.NewGuid():N}";

        var htmlPath = Path.Combine(directory, baseName + ".html");
        var textPath = Path.Combine(directory, baseName + ".txt");

        await File.WriteAllTextAsync(htmlPath, message.HtmlBody, cancellationToken);
        await File.WriteAllTextAsync(textPath, message.TextBody, cancellationToken);

        logger.LogInformation(
            "Email {Purpose} to {To} ({Subject}) written to {Path}",
            message.Purpose,
            message.To,
            message.Subject,
            htmlPath);
    }

    private string ResolveDirectory() =>
        string.IsNullOrWhiteSpace(settings.File.Directory)
            ? Path.Combine(Path.GetTempPath(), "nepalhub-emails")
            : settings.File.Directory;

    /// <summary>
    /// Keeps the file name to letters, digits and dashes. The purpose is one of ours, but a
    /// path built from a value that could ever come from elsewhere is a path that eventually
    /// will.
    /// </summary>
    private static string Sanitize(string purpose)
    {
        var builder = new StringBuilder(purpose.Length);

        foreach (var character in purpose)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '-');
        }

        return builder.Length > 0 ? builder.ToString() : "email";
    }
}
