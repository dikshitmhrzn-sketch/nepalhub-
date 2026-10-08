using Microsoft.AspNetCore.Identity;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Email;

/// <summary>
/// Turns an account event into a message a person can act on: Identity mints the token, the
/// link builder gives it the deployment's own origin, the templates give it a body, and the
/// configured sender carries it away.
///
/// <para>
/// A provider that fails is a log line, never a failed request. The account — registered,
/// found, about to be reset — already exists by the time this runs, and answering the caller
/// with "the mail server is down" would only tell an attacker when mail infrastructure
/// breaks while telling a real person nothing they can act on. They can always ask again,
/// and the resend and forgot endpoints answer the same either way.
/// </para>
/// </summary>
public sealed class EmailDispatchService(
    UserManager<ApplicationUser> userManager,
    EmailSettings settings,
    EmailLinkBuilder linkBuilder,
    IEmailSender sender,
    ILogger<EmailDispatchService> logger)
{
    public Task SendVerificationAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        DispatchAsync(user, "verify-email", CreateVerificationDraftAsync, cancellationToken);

    public Task SendPasswordResetAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        DispatchAsync(user, "reset-password", CreatePasswordResetDraftAsync, cancellationToken);

    public Task SendPasswordChangedNoticeAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        DispatchAsync(
            user,
            "password-changed",
            draftUser => Task.FromResult(EmailTemplates.PasswordChanged(draftUser.FullName)),
            cancellationToken);

    private async Task<EmailDraft> CreateVerificationDraftAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

        return EmailTemplates.VerifyEmail(
            user.FullName,
            linkBuilder.BuildVerifyEmailLink(EmailOf(user), token),
            settings.ConfirmTokenLifetime);
    }

    private async Task<EmailDraft> CreatePasswordResetDraftAsync(ApplicationUser user)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        return EmailTemplates.ResetPassword(
            user.FullName,
            linkBuilder.BuildResetPasswordLink(EmailOf(user), token),
            settings.ResetTokenLifetime);
    }

    private async Task DispatchAsync(
        ApplicationUser user,
        string purpose,
        Func<ApplicationUser, Task<EmailDraft>> draftFactory,
        CancellationToken cancellationToken)
    {
        var email = EmailOf(user);
        if (email.Length == 0)
        {
            logger.LogError(
                "Cannot send the {Purpose} email for user {UserId}: the account has no address.",
                purpose,
                user.Id);
            return;
        }

        try
        {
            // Inside the guard as well: building the link needs the configured origin, and
            // an endpoint whose mail section is half-configured must still answer — a host
            // that cannot even form a link is a host that simply does not send.
            var draft = await draftFactory(user);

            await sender.SendAsync(
                new EmailMessage(email, draft.Subject, draft.HtmlBody, draft.TextBody, purpose),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not send the {Purpose} email to {Email}.", purpose, email);
        }
    }

    private static string EmailOf(ApplicationUser user) => user.Email?.Trim().ToLowerInvariant() ?? string.Empty;
}
