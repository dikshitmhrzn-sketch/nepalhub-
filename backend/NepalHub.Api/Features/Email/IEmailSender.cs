namespace NepalHub.Api.Features.Email;

/// <summary>
/// Where a delivery provider attaches. One implementation is registered from configuration;
/// nothing outside this folder knows which one is live, so swapping SMTP for another transport
/// is a registration change rather than a rewrite of verification or recovery.
///
/// <para>
/// The contract carries no credentials: those live in configuration bound from user-secrets or
/// the environment, and a message that fails to send is a log line, never a reason an endpoint
/// stops answering. An endpoint that has already created or found the account answers as if the
/// mail were on its way — the user can always ask again, and telling a caller "no mail" would
/// tell them whether the account exists.
/// </para>
/// </summary>
public interface IEmailSender
{
    /// <summary>Hands one message to the provider. Failures are logged by the caller, not thrown at it.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// One message, fully rendered. The body arrives as HTML and plain text together because a
/// mail client — and every security-conscious mail gateway — prefers to see both, and a
/// message with only HTML reads as nothing at all to the rest.
///
/// <para>
/// <paramref name="Purpose"/> is a stable machine name for what the message is for
/// (<c>verify-email</c>, <c>reset-password</c>): it names the file the development sender
/// writes and the field the log records, so a developer can find the message they just
/// triggered without opening all of them.
/// </para>
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    string Purpose);
