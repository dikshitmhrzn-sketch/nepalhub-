using Microsoft.Extensions.Logging.Abstractions;

namespace NepalHub.Api.Features.Email;

/// <summary>
/// The sender a host with no provider configured gets: it accepts everything and sends
/// nothing, so the features that offer to email someone still behave — the flow completes,
/// the endpoint answers, and the only difference is that no message leaves the machine.
///
/// <para>
/// This is what keeps the default install and the test host on their old behaviour: with
/// <c>Email:Provider=none</c> accounts start confirmed, nobody is asked to verify, and no
/// test has to stand up a mail server to sign in.
/// </para>
/// </summary>
public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger =
        NullLogger<NullEmailSender>.Instance;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Email {Purpose} to {To} not sent: no provider is configured on this host.",
            message.Purpose,
            message.To);

        return Task.CompletedTask;
    }
}
