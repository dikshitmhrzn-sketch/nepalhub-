using Microsoft.AspNetCore.Identity;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Data.Seeding;

/// <summary>
/// Creates or promotes one development administrator, so a fresh checkout can reach the staff
/// screens without a hand-written row in Identity's tables or an admin granting itself the role
/// it does not have yet.
///
/// <para>
/// The account is built the same way registration builds one: <c>UserManager.CreateAsync</c>
/// hashes the password through ASP.NET Core Identity, so the row it writes and the check the
/// sign-in endpoint runs are the application's own. Nothing here bypasses hashing, weakens a
/// policy or grants a role to an account it was not asked about.
/// </para>
///
/// <para>
/// It is driven entirely by configuration and refuses to run outside Development. With no
/// <c>DevAdmin:Email</c> configured - every fresh clone, the test host and production - it does
/// nothing at all, and the email and password live in user-secrets or the environment exactly
/// like the connection string, never in a committed file. The password is optional: without it
/// the seeder only promotes an account that was registered through the normal UI, so a machine
/// that would rather not keep a password in configuration still gets an admin.
/// </para>
/// </summary>
internal static class DevAdminSeeder
{
    internal static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var email = configuration["DevAdmin:Email"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            // The ordinary case: nobody asked for a development admin.
            return;
        }

        if (!environment.IsDevelopment())
        {
            logger.LogWarning(
                "DevAdmin:Email is configured outside the Development environment; the development admin was not seeded for {Email}.",
                email);
            return;
        }

        var password = configuration["DevAdmin:Password"];
        var existing = await userManager.FindByEmailAsync(email);

        if (existing is not null)
        {
            await EnsureAdminAsync(userManager, existing, email, logger, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            // No password in configuration: the account is made the way every other account is
            // made, through registration, and promoted at the next startup.
            logger.LogInformation(
                "No account for {Email} yet. Register it through the UI and restart the API to grant it the admin role.",
                email);
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = configuration["DevAdmin:FullName"]?.Trim() is { Length: > 0 } name
                ? name
                : "Development Admin",
            // Accounts start confirmed because no mail provider exists yet, exactly as
            // POST /api/auth/register does it.
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            var failures = string.Join("; ", created.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Could not seed the development admin for '{email}': {failures}");
        }

        // Created before the role, so an account that somehow loses the second step is promoted
        // on the next startup rather than being left half-finished and silent.
        await EnsureAdminAsync(userManager, user, email, logger, cancellationToken);
    }

    /// <summary>
    /// Grants the admin role to one named account and nothing else. The lookup is by the exact
    /// configured email, so an unrelated account can never be promoted, and an account that
    /// already holds the role is left untouched - the idempotency every other seeder here has.
    /// </summary>
    private static async Task EnsureAdminAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string email,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            logger.LogInformation("Development admin {Email} already holds the admin role.", email);
            return;
        }

        var result = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!result.Succeeded)
        {
            var failures = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Could not grant the admin role to the development admin '{email}': {failures}");
        }

        logger.LogInformation("Granted the admin role to the development admin {Email}.", email);
    }
}
