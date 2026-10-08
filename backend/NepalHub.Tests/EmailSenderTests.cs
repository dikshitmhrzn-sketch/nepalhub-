using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NepalHub.Api.Features.Email;

namespace NepalHub.Tests;

/// <summary>
/// The email abstraction on its own, with no host and no database: what a sender writes,
/// what a link looks like on the way out, how a template treats a name, how long the quiet
/// period holds, and where the settings refuse to start. The one host-level assertion —
/// which sender an unconfigured deployment actually resolves — sits at the bottom.
/// </summary>
public class EmailSenderTests
{
    [Fact]
    public async Task File_sender_writes_the_html_and_the_text_half_side_by_side()
    {
        var directory = NewScratchDirectory();

        try
        {
            var sender = new FileEmailSender(
                new EmailSettings { Provider = EmailSettings.ProviderFile, File = new EmailFileSettings { Directory = directory } },
                NullLogger<FileEmailSender>.Instance);

            await sender.SendAsync(new EmailMessage(
                To: "reader@nepalhub.test",
                Subject: "Confirm your email address for Nepal Hub",
                HtmlBody: "<p>Body in HTML.</p>",
                TextBody: "Body in text.",
                Purpose: "verify-email"));

            var htmlPath = Assert.Single(Directory.EnumerateFiles(directory, "*.html"));
            var textPath = Assert.Single(Directory.EnumerateFiles(directory, "*.txt"));

            Assert.Contains("Body in HTML.", await File.ReadAllTextAsync(htmlPath));
            Assert.Contains("Body in text.", await File.ReadAllTextAsync(textPath));
            Assert.Contains("verify-email", Path.GetFileName(htmlPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Link_builder_percent_encodes_every_character_a_query_string_would_mangle()
    {
        var builder = new EmailLinkBuilder(new EmailSettings
        {
            Provider = EmailSettings.ProviderFile,
            WebBaseUrl = "https://nepalhub.example.com",
        });

        var link = builder.BuildVerifyEmailLink("reader+nepal@nepalhub.test", "ab+cd/ef==");

        Assert.StartsWith("https://nepalhub.example.com/verify-email?email=", link);
        Assert.DoesNotContain("ab+cd", link);
        Assert.Contains("ab%2Bcd%2Fef%3D%3D", link);
        Assert.Contains("reader%2Bnepal%40nepalhub.test", link);
    }

    [Fact]
    public void Link_builder_refuses_to_invent_an_origin_it_was_never_given()
    {
        var builder = new EmailLinkBuilder(new EmailSettings { Provider = EmailSettings.ProviderFile });

        var thrown = Assert.Throws<InvalidOperationException>(
            () => builder.BuildResetPasswordLink("reader@nepalhub.test", "token"));

        Assert.Contains("WebBaseUrl", thrown.Message);
    }

    [Fact]
    public void Templates_turn_a_name_that_looks_like_markup_back_into_text_on_screen()
    {
        var draft = EmailTemplates.VerifyEmail(
            fullName: "Sam & <b>Arya</b>",
            link: "https://nepalhub.example.com/verify-email?email=a%40b.test&token=t",
            lifetime: TimeSpan.FromHours(1));

        Assert.Contains("Sam &amp; &lt;b&gt;Arya&lt;/b&gt;", draft.HtmlBody);
        Assert.DoesNotContain("<b>Arya</b>", draft.HtmlBody);
    }

    [Fact]
    public void Templates_encode_the_link_inside_the_button_but_leave_the_text_half_clickable()
    {
        var link = "https://nepalhub.example.com/reset-password?email=a%40b.test&token=t";

        var draft = EmailTemplates.ResetPassword("Reader", link, TimeSpan.FromHours(1));

        Assert.Contains("email=a%40b.test&amp;token=t", draft.HtmlBody);
        Assert.Contains(link, draft.TextBody);
    }

    [Fact]
    public void Templates_keep_a_name_verbatim_in_the_half_that_shows_no_markup()
    {
        var draft = EmailTemplates.PasswordChanged("Sam & <b>Arya</b>");

        Assert.Contains("Hi Sam & <b>Arya</b>,", draft.TextBody);
    }

    [Fact]
    public void Cooldown_sends_once_then_stays_silent_until_the_window_passes()
    {
        var settings = new EmailSettings
        {
            Provider = EmailSettings.ProviderFile,
            SendCooldownSeconds = 60,
        };

        var clock = new MovableTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var cooldown = new EmailSendCooldown(settings, clock);

        Assert.True(cooldown.TryEnter("Reader@nepalhub.test"));
        Assert.False(cooldown.TryEnter("reader@nepalhub.test"));

        clock.Set(clock.GetUtcNow().AddSeconds(59));
        Assert.False(cooldown.TryEnter("reader@nepalhub.test"));

        clock.Set(clock.GetUtcNow().AddSeconds(2));
        Assert.True(cooldown.TryEnter("reader@nepalhub.test"));
    }

    [Fact]
    public void Settings_refuse_a_provider_they_could_not_actually_answer_with()
    {
        var settings = new EmailSettings { Provider = "sendgrid" };

        Assert.Contains("not supported", settings.Validate(isProduction: false));
    }

    /// <summary>
    /// The development sender needs someone to read what it writes; in Production that
    /// someone does not exist, so the boot stops rather than deploying a site whose
    /// verification mail piles up unread on a disk.
    /// </summary>
    [Fact]
    public void Settings_refuse_the_development_sender_in_production()
    {
        var settings = new EmailSettings
        {
            Provider = EmailSettings.ProviderFile,
            WebBaseUrl = "https://nepalhub.example.com",
        };

        Assert.Contains("refused in Production", settings.Validate(isProduction: true));
        Assert.Null(settings.Validate(isProduction: false));
    }

    [Fact]
    public void Settings_keep_the_default_host_startable_before_any_mail_is_configured()
    {
        var settings = new EmailSettings();

        Assert.Null(settings.Validate(isProduction: true));
        Assert.False(settings.EnforcesVerification);
    }

    [Fact]
    public void Settings_linking_needs_an_origin_before_a_provider_can_send()
    {
        var settings = new EmailSettings { Provider = EmailSettings.ProviderFile };

        Assert.Contains("WebBaseUrl", settings.Validate(isProduction: false));
    }

    /// <summary>
    /// The whole default-install promise in one assertion: with nothing configured, the host
    /// hands out the sender that sends nothing — so no account is ever asked to verify a
    /// message nobody would have received.
    /// </summary>
    [Fact]
    public void An_unconfigured_host_resolves_the_sender_that_sends_nothing()
    {
        using var factory = new NepalHubApiFactory();

        Assert.IsType<NullEmailSender>(factory.Services.GetRequiredService<IEmailSender>());
    }

    /// <summary>
    /// The production provider, once every part the relay needs is described: host to
    /// connect to, a From the receiver can answer, an origin for the links inside.
    /// </summary>
    [Fact]
    public void Settings_accept_the_smtp_provider_when_the_relay_is_fully_described()
    {
        Assert.Null(SmtpReady().Validate(isProduction: true));
    }

    [Fact]
    public void Settings_explain_which_smtp_part_the_connection_would_be_missing()
    {
        var noHost = SmtpReady();
        noHost.Smtp.Host = " ";
        Assert.Contains("Smtp:Host", noHost.Validate(isProduction: true));

        var badPort = SmtpReady();
        badPort.Smtp.Port = 0;
        Assert.Contains("Smtp:Port", badPort.Validate(isProduction: true));

        var badEncryption = SmtpReady();
        badEncryption.Smtp.Encryption = "opportunistic-maybe";
        Assert.Contains("Smtp:Encryption", badEncryption.Validate(isProduction: true));
    }

    /// <summary>
    /// A message with no From is one a relay either refuses or rewrites beyond recognition,
    /// and credentials supplied in half are a secret rotated to a value that no longer works.
    /// </summary>
    [Fact]
    public void Settings_explain_which_smtp_part_the_envelope_would_be_missing()
    {
        var noFrom = SmtpReady();
        noFrom.From = string.Empty;
        Assert.Contains("Email:From", noFrom.Validate(isProduction: true));

        var passwordAlone = SmtpReady();
        passwordAlone.Smtp.Password = "half-a-credential";
        Assert.Contains("must be set together", passwordAlone.Validate(isProduction: true));

        var userAlone = SmtpReady();
        userAlone.Smtp.UserName = "half-a-credential";
        Assert.Contains("must be set together", userAlone.Validate(isProduction: true));
    }

    /// <summary>
    /// The one host-level assertion for SMTP: with the mail section pointed at a relay, the
    /// host resolves the sender that speaks SMTP — and still never opens a socket to prove it.
    ///
    /// <para>
    /// The keys travel as environment variables rather than an in-memory configuration
    /// source: the application binds its mail section while it is still building, before a
    /// fixture's own source would be applied, which is the same reason every other
    /// startup-time override in this suite (<c>ConnectionStrings__Default</c>,
    /// <c>Payments__ReconcileIntervalSeconds</c>) is an environment variable too. The suite
    /// runs serialized, so setting them around this one host and clearing them in the
    /// finally means no other host is ever built while they are set.
    /// </para>
    /// </summary>
    [Fact]
    public void A_host_configured_for_smtp_resolves_the_smtp_sender()
    {
        var mail = new Dictionary<string, string?>
        {
            ["Email__Provider"] = EmailSettings.ProviderSmtp,
            ["Email__WebBaseUrl"] = "https://nepalhub.example.com",
            ["Email__From"] = "no-reply@nepalhub.test",
            ["Email__Smtp__Host"] = "smtp.example.test",
        };

        try
        {
            foreach (var (key, value) in mail)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            using var factory = new NepalHubApiFactory();

            Assert.IsType<SmtpEmailSender>(factory.Services.GetRequiredService<IEmailSender>());
        }
        finally
        {
            foreach (var key in mail.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }

    private static EmailSettings SmtpReady() => new()
    {
        Provider = EmailSettings.ProviderSmtp,
        WebBaseUrl = "https://nepalhub.example.com",
        From = "no-reply@nepalhub.test",
        Smtp = new EmailSmtpSettings { Host = "smtp.example.test" },
    };

    private static string NewScratchDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nepalhub-email-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        return directory;
    }
}
