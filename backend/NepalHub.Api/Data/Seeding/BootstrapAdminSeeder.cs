using Microsoft.AspNetCore.Identity;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Data.Seeding;

/// <summary>
/// Creates or promotes the one administrator that lets a live deployment be operated at all.
/// Every management screen - publishing a job, approving a listing, granting a role - sits
/// behind an account that already holds the admin role, and on a fresh production database no
/// such account exists: <see cref="DevAdminSeeder"/> deliberately refuses to run outside
/// Development, and the role-grant endpoint requires an administrator to call it. This seeder
/// is the single deliberate way out of that circle, and it is driven entirely by configuration
/// so the choice to use it belongs to the operator.
///
/// <para>
/// Three shapes are supported, all reached by setting <c>BootstrapAdmin:Email</c>: an account
/// already registered through the UI is promoted (the recommended path, because no password
/// ever enters the environment); an account that does not exist yet is created when
/// <c>BootstrapAdmin:Password</c> is also set, hashed through <c>UserManager</c> exactly as
/// registration hashes one; and with neither present the seeder only logs what to do next.
/// </para>
///
/// <para>
/// When a password is configured for an account that already exists, the password is checked
/// against that account before any role is granted. Registration does not require mail
/// confirmation on a host with no mail provider, so anybody could have registered the
/// configured address first - a mismatch proves this is not the operator's account and the
/// grant is refused with a log line rather than handed to a squatter.
/// </para>
///
/// <para>
/// While the variable remains set the seeder logs a warning on every boot, because a grant
/// that silently re-applies forever is a standing administrator nobody remembers creating.
/// Removing <c>BootstrapAdmin:Email</c> after the first sign-in is the second half of the
/// procedure, and the warning is how an operator finds out they skipped it.
/// </para>
/// </summary>
internal static class BootstrapAdminSeeder
{
    internal const string EmailConfigurationKey = "BootstrapAdmin:Email";

    internal const string PasswordConfigurationKey = "BootstrapAdmin:Password";

    internal const string FullNameConfigurationKey = "BootstrapAdmin:FullName";

    internal static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var email = configuration[EmailConfigurationKey]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            // The ordinary case: nobody is bootstrapping an administrator right now.
            return;
        }

        // The variable's job is not over when the account exists. Left in place it would
        // re-grant a role that an operator may later deliberately revoke, so staying
        // configured is itself worth saying out loud until it is removed.
        logger.LogWarning(
            "{Key} is still configured. The admin account is in place - remove the variable and redeploy so this grant cannot silently re-apply.",
            EmailConfigurationKey);

        var password = configuration[PasswordConfigurationKey];
        var existing = await userManager.FindByEmailAsync(email);

        if (existing is not null)
        {
            await PromoteExistingAsync(
                userManager,
                existing,
                email,
                password,
                logger,
                cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            // The recommended path: the account is made through the normal registration flow,
            // so the only secret in configuration is an address, and the next boot promotes it.
            logger.LogInformation(
                "No account for {Email} yet. Register it through the UI, then restart the API (or redeploy) to grant it the admin role.",
                email);
            return;
        }

        await CreateAsync(userManager, email, password, configuration, logger, cancellationToken);
    }

    /// <summary>
    /// Grants the admin role to one named account and nothing else. The lookup is the exact
    /// configured email, so an unrelated account can never be promoted, and an account that
    /// already holds the role is left untouched - the idempotency every other seeder here has.
    /// When a password accompanies the address it must sign in to the existing account first:
    /// on a host where registration needs no mail confirmation, proving possession of the
    /// password is the only proof that this account belongs to the operator.
    /// </summary>
    private static async Task PromoteExistingAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string email,
        string? password,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(password)
            && !await userManager.CheckPasswordAsync(user, password))
        {
            logger.LogWarning(
                "An account for {Email} already exists but the configured {PasswordKey} does not sign in to it. The admin role was NOT granted: this is either not your account or not your password. Remove the bootstrap variables and start over.",
                email,
                PasswordConfigurationKey);
            return;
        }

        if (await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            logger.LogInformation("The bootstrap admin {Email} already holds the admin role.", email);
            return;
        }

        var result = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!result.Succeeded)
        {
            var failures = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Could not grant the admin role to the bootstrap admin '{email}': {failures}");
        }

        logger.LogInformation(
            "Granted the admin role to the bootstrap admin {Email}. Remove {Key} from the environment and redeploy.",
            email,
            EmailConfigurationKey);
    }

    /// <summary>
    /// Builds the account the way registration builds one: <c>UserManager.CreateAsync</c>
    /// hashes the password through ASP.NET Core Identity, so the row it writes and the check
    /// the sign-in endpoint runs are the application's own. The account is created before the
    /// role for the same reason the development seeder does it - a step that fails between the
    /// two is retried on the next boot rather than left half-finished and silent.
    /// </summary>
    private static async Task CreateAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = configuration[FullNameConfigurationKey]?.Trim() is { Length: > 0 } name
                ? name
                : "Site Admin",
            // Confirmed without a mail check, exactly as registration does it when no mail
            // provider is configured - the address came from the operator's own environment.
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            var failures = string.Join("; ", created.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Could not create the bootstrap admin for '{email}': {failures}");
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!roleResult.Succeeded)
        {
            var failures = string.Join("; ", roleResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Could not grant the admin role to the bootstrap admin '{email}': {failures}");
        }

        logger.LogInformation(
            "Created the bootstrap admin {Email} and granted it the admin role. Remove {Key} from the environment and redeploy.",
            email,
            EmailConfigurationKey);
    }
}
