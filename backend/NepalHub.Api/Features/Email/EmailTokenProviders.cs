using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Email;

/// <summary>
/// Issues the tokens that confirm an email address, with its lifetime read from the mail
/// settings rather than Identity's one-size default.
///
/// <para>
/// Identity's own provider encrypts a creation stamp, the user, the purpose and the security
/// stamp under a protector named after <c>Name</c>; this subclass changes nothing about that
/// except how long the stamp stays inside the window — the confirmation link is worth a day,
/// and it is worth exactly the configured day.
/// </para>
/// </summary>
public sealed class EmailConfirmationTokenProvider : DataProtectorTokenProvider<ApplicationUser>
{
    public const string ProviderName = "EmailConfirmation";

    public EmailConfirmationTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        EmailSettings settings,
        ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
        : base(dataProtectionProvider, BuildOptions(settings.ConfirmTokenLifetime), logger)
    {
    }

    /// <summary>The window this provider gives a token, so the wiring itself is assertable.</summary>
    public TimeSpan Lifespan => Options.TokenLifespan;

    private static IOptions<DataProtectionTokenProviderOptions> BuildOptions(TimeSpan lifespan) =>
        new OptionsWrapper<DataProtectionTokenProviderOptions>(new DataProtectionTokenProviderOptions
        {
            Name = ProviderName,
            TokenLifespan = lifespan,
        });
}

/// <summary>
/// Issues the tokens that reset a password, on the deliberately shorter clock: a reset link
/// is the one that hands over an account, so it is the one worth the least time lying in an
/// inbox. Every other part of the mechanism — the encrypted stamp, the security-stamp check
/// that makes it single-use the moment the password changes — is Identity's own.
/// </summary>
public sealed class PasswordResetTokenProvider : DataProtectorTokenProvider<ApplicationUser>
{
    public const string ProviderName = "PasswordReset";

    public PasswordResetTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        EmailSettings settings,
        ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
        : base(dataProtectionProvider, BuildOptions(settings.ResetTokenLifetime), logger)
    {
    }

    /// <summary>The window this provider gives a token, so the wiring itself is assertable.</summary>
    public TimeSpan Lifespan => Options.TokenLifespan;

    private static IOptions<DataProtectionTokenProviderOptions> BuildOptions(TimeSpan lifespan) =>
        new OptionsWrapper<DataProtectionTokenProviderOptions>(new DataProtectionTokenProviderOptions
        {
            Name = ProviderName,
            TokenLifespan = lifespan,
        });
}
