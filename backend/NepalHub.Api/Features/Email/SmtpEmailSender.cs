using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NepalHub.Api.Features.Email;

/// <summary>
/// The sender that leaves the building: MailKit speaks the wire protocol the world's mail
/// providers expect — the obsolete <c>System.Net.Mail.SmtpClient</c> is the one Microsoft
/// stopped developing — and hands off like every other transport here behind
/// <see cref="IEmailSender"/>, so nothing else in the module knows or cares that SMTP is
/// what answered.
///
/// <para>
/// A new connection per message rather than a pooled one: transactional mail is rare enough
/// that the handshake costs nothing worth managing, and a pooled connection would need a
/// lifetime, a reconnect policy and a place to notice that the relay went away — none of
/// which a message a user is waiting for should depend on. Connection failures propagate to
/// the caller, which logs them and answers the request anyway: a relay being down must never
/// be the reason a person cannot reset their password or see a "check your email" page.
/// </para>
/// </summary>
public sealed class SmtpEmailSender(EmailSettings settings, ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = settings.Smtp;

        var mime = new MimeMessage
        {
            Subject = message.Subject,
            Body = new BodyBuilder
            {
                HtmlBody = message.HtmlBody,
                TextBody = message.TextBody,
            }.ToMessageBody(),
        };

        mime.From.Add(new MailboxAddress(settings.FromName, settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));

        using var client = new SmtpClient();

        await client.ConnectAsync(
            smtp.Host,
            smtp.Port,
            ToSecureSocketOptions(smtp.Encryption),
            cancellationToken);

        try
        {
            if (smtp.UserName.Length > 0)
            {
                await client.AuthenticateAsync(smtp.UserName, smtp.Password, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
        }
        finally
        {
            // Even after a failed send, so the relay sees a clean QUIT instead of a dropped
            // socket it then has to time out on its side.
            await client.DisconnectAsync(quit: true, CancellationToken.None);
        }

        logger.LogInformation(
            "Email {Purpose} to {To} sent through {Host}:{Port}",
            message.Purpose,
            message.To,
            smtp.Host,
            smtp.Port);
    }

    private static SecureSocketOptions ToSecureSocketOptions(string encryption) =>
        encryption.Trim().ToLowerInvariant() switch
        {
            "ssl" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto,
        };
}
