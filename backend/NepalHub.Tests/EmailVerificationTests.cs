using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Email;

namespace NepalHub.Tests;

/// <summary>
/// Verification as a person meets it: register while mail is configured, receive a link,
/// open it, sign in — plus the answers the endpoints owe an anonymous stranger who is
/// probing which addresses exist, which are always the same answer.
///
/// <para>
/// The host runs with the development provider so verification is actually enforced, and
/// the sender is swapped for one that records instead of sending: no socket, no mailbox,
/// and the link under test is read from the very message the site would have mailed.
/// </para>
/// </summary>
public class EmailVerificationTests
{
    [Fact]
    public async Task Registering_with_mail_configured_creates_the_account_unconfirmed_and_sends_the_link()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using var response = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var registered = await response.Content.ReadFromJsonAsync<ApiEnvelope<SessionUserResponse>>(TestJson.Options);
        Assert.NotNull(registered?.Data);
        Assert.False(registered!.Data!.IsEmailConfirmed);

        var message = Assert.Single(host.Recorder.Messages);
        Assert.Equal("verify-email", message.Purpose);
        Assert.Equal(email, message.To);
        Assert.Contains("Confirm your email address", message.Subject);

        // No cookie was minted: the account exists, but nobody is signed in as it yet.
        var session = await api.GetAsync<SessionResponse>("/api/auth/session");
        Assert.False(session.Authenticated);
    }

    [Fact]
    public async Task The_link_in_the_mail_confirms_the_account_and_repeating_it_still_answers_well()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma");
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var link = EmailLinks.Read(Assert.Single(host.Recorder.Messages));

        using var confirmed = await api.PostAsync("/api/auth/verify-email", new { link.Email, link.Token });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        // The back button, a prefetcher, a doubled click: the same link lands on the same
        // good answer instead of an error the user did nothing to cause.
        using var again = await api.PostAsync("/api/auth/verify-email", new { link.Email, link.Token });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        using var login = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Sign_in_refuses_an_unconfirmed_account_only_when_the_password_matches()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma");
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var refused = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var refusedBody = await refused.Content.ReadAsStringAsync();
        Assert.Contains("email_not_confirmed", refusedBody);

        // A wrong password answers exactly as it would for an address nobody holds: the
        // reason is only ever shown to someone who has already proved they know the door.
        using var wrongPassword = await api.LoginAsync(email, "NotThePassword!");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync();
        Assert.Contains("invalid_credentials", wrongPasswordBody);
        Assert.DoesNotContain("email_not_confirmed", wrongPasswordBody);

        var link = EmailLinks.Read(Assert.Single(host.Recorder.Messages));
        using var confirmed = await api.PostAsync("/api/auth/verify-email", new { link.Email, link.Token });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        using var allowed = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Resend_answers_identically_whether_or_not_the_address_is_known()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma");
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Single(host.Recorder.Messages);

        using var known = await api.PostAsync("/api/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        var knownBody = await known.Content.ReadAsStringAsync();
        Assert.Equal(2, host.Recorder.Messages.Count);

        using var unknown = await api.PostAsync(
            "/api/auth/resend-verification",
            new { email = TestData.UniqueEmail("stranger") });
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(knownBody, await unknown.Content.ReadAsStringAsync());
        Assert.Equal(2, host.Recorder.Messages.Count);

        var link = EmailLinks.Read(host.Recorder.Messages[0]);
        using var confirmed = await api.PostAsync("/api/auth/verify-email", new { link.Email, link.Token });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        using var alreadyConfirmed = await api.PostAsync("/api/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.OK, alreadyConfirmed.StatusCode);
        Assert.Equal(knownBody, await alreadyConfirmed.Content.ReadAsStringAsync());
        Assert.Equal(2, host.Recorder.Messages.Count);
    }

    [Fact]
    public async Task A_bad_token_and_an_unknown_address_get_the_same_answer()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());

        using var badToken = await api.PostAsync(
            "/api/auth/verify-email",
            new { email = TestData.UniqueEmail(), token = "not-a-token-at-all" });
        Assert.Equal(HttpStatusCode.BadRequest, badToken.StatusCode);
        var badTokenBody = await badToken.Content.ReadAsStringAsync();
        Assert.Contains("token_invalid", badTokenBody);

        using var unknownAddress = await api.PostAsync(
            "/api/auth/verify-email",
            new { email = TestData.UniqueEmail(), token = "not-a-token-at-all" });
        Assert.Equal(HttpStatusCode.BadRequest, unknownAddress.StatusCode);
        Assert.Equal(badTokenBody, await unknownAddress.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_sender_that_cannot_send_never_fails_the_registration()
    {
        using var host = MailApiFactory.Create();
        host.Recorder.Failure = new InvalidOperationException("the relay is down");
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using var response = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma");

        // The account exists either way: a person asking to join must not be told "mail
        // server unavailable" when the server is the only thing that is unavailable.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(host.Recorder.Messages);
    }

    /// <summary>
    /// The two providers Identity mints tokens with run on the clocks the mail settings
    /// name — a confirmation link worth a day, a reset link worth an hour — rather than
    /// Identity's one-size default of a day for everything.
    /// </summary>
    [Fact]
    public void The_token_providers_run_on_their_configured_clocks()
    {
        using var host = new NepalHubApiFactory();

        var confirmation = host.Services.GetRequiredService<EmailConfirmationTokenProvider>();
        var reset = host.Services.GetRequiredService<PasswordResetTokenProvider>();

        Assert.Equal(TimeSpan.FromDays(1), confirmation.Lifespan);
        Assert.Equal(TimeSpan.FromMinutes(60), reset.Lifespan);
    }
}

/// <summary>
/// A host with the development provider configured — which is what makes verification
/// binding rather than optional — and a sender that records instead of sending.
/// <para>
/// The mail section is bound while the application starts, before any in-memory
/// configuration source a fixture could add would apply, so the provider travels as
/// environment variables for the length of the startup and is cleared the moment the host
/// is up — the same reason every other startup-time override in this suite travels that
/// way. The suite runs serialized, so no other host is built inside that window.
/// </para>
/// </summary>
public sealed class MailApiFactory : NepalHubApiFactory
{
    public static MailApiFactory Create()
    {
        var mail = new Dictionary<string, string?>
        {
            ["Email__Provider"] = EmailSettings.ProviderFile,
            ["Email__WebBaseUrl"] = "https://nepalhub.test",
        };

        try
        {
            foreach (var (key, value) in mail)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            return new MailApiFactory();
        }
        finally
        {
            foreach (var key in mail.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }

    public MailApiFactory()
    {
        Recorder = new RecordingEmailSender();

        // The host must start while the environment still names the provider, or its
        // mail section would bind the defaults — the same reason the quota fixtures
        // start themselves inside the window they pin their settings in.
        _ = Services;
    }

    public RecordingEmailSender Recorder { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Recorder);
        });
    }
}

/// <summary>Pulls the one URL out of a message's text half, as a mail reader would offer it.</summary>
public static class EmailLinks
{
    public static (string Email, string Token) Read(EmailMessage message)
    {
        var link = message.TextBody
            .Split('\n')
            .Single(line => line.StartsWith("https://", StringComparison.Ordinal));

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in link[(link.IndexOf('?') + 1)..].Split('&'))
        {
            var parts = pair.Split('=', 2);
            values[parts[0]] = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return (values["email"], values["token"]);
    }
}

/// <summary>The quiet stand-in for a transport: every message kept, failures on demand.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _messages = [];

    public Exception? Failure { get; set; }

    public IReadOnlyList<EmailMessage> Messages
    {
        get
        {
            lock (_messages)
            {
                return [.. _messages];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        // Recorded first, so a test can see what would have gone out even when the
        // point of the test is that the failure must not surface.
        lock (_messages)
        {
            _messages.Add(message);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.CompletedTask;
    }
}
