namespace NepalHub.Api.Features.Email;

/// <summary>
/// Where mail leaves the building, and whether an account may be used before it has proved it
/// owns the address it registered with.
///
/// <para>
/// The section is bound like <c>Payments</c>: the committed files carry only the switches and
/// the public addresses, while anything shaped like a credential (<c>Smtp:Password</c>) is bound
/// from user-secrets or the environment and never appears in a diff. The provider defaults to
/// <c>none</c>, which keeps a fresh clone, the test host and an existing deployment behaving
/// exactly as they did before this module existed — accounts start confirmed and nothing is
/// sent — so enabling mail is a deliberate act rather than an upgrade that changes login.
/// </para>
/// </summary>
public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public const string ProviderNone = "none";

    public const string ProviderFile = "file";

    public const string ProviderSmtp = "smtp";

    /// <summary>Which sender answers. "none" until one is deliberately configured.</summary>
    public string Provider { get; set; } = ProviderNone;

    /// <summary>The envelope's From address. A mailbox on the sending domain, not a secret.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>The name shown beside the address in a mail client's From line.</summary>
    public string FromName { get; set; } = "Nepal Hub";

    /// <summary>
    /// The public origin of the site, where links in mail must point: this is configuration
    /// rather than a request header, because the address a verification link opens on is the
    /// one the deployment serves the SPA from and no caller gets a say in it.
    /// </summary>
    public string WebBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether a new account must confirm its address before it can sign in. Only takes effect
    /// while a provider is configured: with no way to send the link, enforcing it would lock
    /// every new account out of the site it just joined.
    /// </summary>
    public bool RequireVerified { get; set; } = true;

    /// <summary>How long a verification link stays valid.</summary>
    public int ConfirmTokenLifetimeMinutes { get; set; } = 60 * 24;

    /// <summary>
    /// How long a password-reset link stays valid. Deliberately shorter than verification:
    /// a reset link is the one that hands over the account, so it is the one worth the least
    /// time lying in an inbox.
    /// </summary>
    public int ResetTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// The quiet period per address between two sends, so a loop cannot turn one mailbox into
    /// a flood. Held in memory, which is the whole deployment model: one host, one process.
    /// </summary>
    public int SendCooldownSeconds { get; set; } = 60;

    public EmailFileSettings File { get; set; } = new();

    public EmailSmtpSettings Smtp { get; set; } = new();

    /// <summary>Whether any sender is configured at all.</summary>
    public bool HasProvider =>
        !string.IsNullOrWhiteSpace(Provider)
        && !string.Equals(Provider, ProviderNone, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether registration and sign-in will demand a confirmed address.</summary>
    public bool EnforcesVerification => HasProvider && RequireVerified;

    public TimeSpan ConfirmTokenLifetime => TimeSpan.FromMinutes(ConfirmTokenLifetimeMinutes);

    public TimeSpan ResetTokenLifetime => TimeSpan.FromMinutes(ResetTokenLifetimeMinutes);

    public TimeSpan SendCooldown => TimeSpan.FromSeconds(SendCooldownSeconds);

    /// <summary>
    /// Checks the section before the host starts, so a bad setting stops the boot with one
    /// sentence instead of surfacing later as mail that silently never arrives or as an
    /// account that cannot be signed into. Returns the problem, or null when the section is
    /// usable in the given environment.
    /// </summary>
    public string? Validate(bool isProduction)
    {
        var isFile = string.Equals(Provider, ProviderFile, StringComparison.OrdinalIgnoreCase);
        var isSmtp = string.Equals(Provider, ProviderSmtp, StringComparison.OrdinalIgnoreCase);

        if (!isFile
            && !isSmtp
            && !string.Equals(Provider, ProviderNone, StringComparison.OrdinalIgnoreCase))
        {
            return $"Email:Provider '{Provider}' is not supported. Set it to one of: none, file, smtp.";
        }

        if (isProduction && isFile)
        {
            return "Email:Provider 'file' writes messages to disk for local development and is "
                + "refused in Production, where nobody would ever read them. Use smtp or none.";
        }

        if (HasProvider
            && (!Uri.TryCreate(WebBaseUrl, UriKind.Absolute, out var origin)
                || (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps)))
        {
            return "Email:WebBaseUrl must be the absolute http(s) origin of the site (for "
                + "example https://nepalhub.example.com) while a provider is configured, because "
                + "every link in an email is built from it.";
        }

        if (isSmtp)
        {
            if (string.IsNullOrWhiteSpace(Smtp.Host))
            {
                return "Email:Smtp:Host is required when the provider is smtp.";
            }

            if (Smtp.Port is < 1 or > 65535)
            {
                return "Email:Smtp:Port must be between 1 and 65535.";
            }

            if (!EmailSmtpSettings.EncryptionOptions.Contains(
                    Smtp.Encryption.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                return $"Email:Smtp:Encryption '{Smtp.Encryption}' is not supported. Set it to one of: "
                    + string.Join(", ", EmailSmtpSettings.EncryptionOptions) + ".";
            }

            if (string.IsNullOrWhiteSpace(From))
            {
                return "Email:From is required when the provider is smtp, because a message "
                    + "without an envelope the receiver can answer is one that gets dropped.";
            }

            var hasUser = !string.IsNullOrWhiteSpace(Smtp.UserName);
            var hasPassword = Smtp.Password is { Length: > 0 };

            if (hasUser != hasPassword)
            {
                return "Email:Smtp:UserName and Email:Smtp:Password must be set together — an "
                    + "unauthenticated relay needs neither, a provider account needs both.";
            }
        }

        if (ConfirmTokenLifetimeMinutes < 1)
        {
            return "Email:ConfirmTokenLifetimeMinutes must be at least 1.";
        }

        if (ResetTokenLifetimeMinutes < 1)
        {
            return "Email:ResetTokenLifetimeMinutes must be at least 1.";
        }

        if (SendCooldownSeconds < 0)
        {
            return "Email:SendCooldownSeconds cannot be negative.";
        }

        return null;
    }
}

/// <summary>Where the development sender drops the mail it would otherwise hand to SMTP.</summary>
public sealed class EmailFileSettings
{
    /// <summary>
    /// Target directory. Empty means the platform's temp folder under <c>nepalhub-emails</c>,
    /// which needs no setup on any machine a developer is likely to use.
    /// </summary>
    public string Directory { get; set; } = string.Empty;
}

/// <summary>
/// How to reach the SMTP relay: host, port, whether the conversation is or becomes
/// encrypted, and the credentials if the relay wants them — bound from user-secrets or the
/// environment like every other credential in this codebase, so the host and the port are
/// all the committed files ever carry.
/// </summary>
public sealed class EmailSmtpSettings
{
    /// <summary>The relay's hostname, for example <c>smtp.provider.example</c>.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>587 for STARTTLS submission, 465 for implicit TLS — the two the world uses.</summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// How the connection is protected: <c>auto</c> upgrades when the server offers it,
    /// <c>starttls</c> insists on the upgrade, <c>ssl</c> is TLS from the first byte
    /// (port 465), <c>none</c> is for a local relay on a trusted machine.
    /// </summary>
    public string Encryption { get; set; } = "auto";

    /// <summary>The relay account, empty for an unauthenticated local relay.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The relay account's password or API key. From secrets or the environment only.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The accepted spellings of <see cref="Encryption"/>, shared with validation.</summary>
    internal static readonly string[] EncryptionOptions = ["auto", "starttls", "ssl", "none"];
}
