using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Data.Seeding;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

/// <summary>
/// The one account a live deployment is operated through. On a fresh production database no
/// administrator exists - <see cref="DevAdminSeeder"/> refuses to run outside Development and
/// the role-grant endpoint needs an administrator to call it - so these tests pin every shape
/// of the bootstrap: create when asked with a password, promote an account the operator has
/// already registered, refuse when the configured password proves the account is not theirs,
/// and stay a no-op whenever the variable is absent. Each test runs inside a transaction that
/// is rolled back, because the shared test database must not accumulate administrator accounts
/// that later tests would meet.
/// </summary>
public sealed class BootstrapAdminSeederTests(NepalHubApiFactory factory)
    : IClassFixture<NepalHubApiFactory>
{
    private const string CreatePassword = "MemberPass1!";

    /// <summary>
    /// The ordinary case on every boot: no variable configured. Nothing is created and the
    /// existing members of the site are exactly as they were, which is what makes it safe to
    /// call this seeder unconditionally from startup.
    /// </summary>
    [Fact]
    public async Task Without_configuration_nothing_is_created_or_promoted()
    {
        await RunInTransactionAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = services.GetRequiredService<AppDbContext>();

            var member = await CreateMemberAsync(userManager, TestData.UniqueEmail("untouched"));
            var usersBefore = await dbContext.Users.CountAsync();

            await SeedAsync(userManager, Configuration());

            var reloaded = await userManager.FindByEmailAsync(member.Email!);
            Assert.NotNull(reloaded);
            Assert.Equal(usersBefore, await dbContext.Users.CountAsync());
            Assert.False(
                await userManager.IsInRoleAsync(reloaded!, Roles.Admin),
                "an account must not gain the admin role when no bootstrap email is configured");
        });
    }

    /// <summary>
    /// The one-shot path: email and password both configured and no account for the address
    /// yet. The account arrives exactly as registration would build it - password hashed by
    /// Identity, address confirmed without a mail check - and holds the admin role.
    /// </summary>
    [Fact]
    public async Task With_email_and_password_a_missing_account_is_created_as_admin()
    {
        await RunInTransactionAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var email = TestData.UniqueEmail("bootstrap-create");
            const string password = "AdminPass1!";

            await SeedAsync(userManager, Configuration(email, password));

            var created = await userManager.FindByEmailAsync(email);
            Assert.NotNull(created);
            Assert.True(
                await userManager.CheckPasswordAsync(created!, password),
                "the configured password must sign in to the account the seeder created");
            Assert.True(created!.EmailConfirmed, "the operator's own address needs no mail check");
            Assert.Equal("Site Admin", created.FullName);
            Assert.True(
                await userManager.IsInRoleAsync(created, Roles.Admin),
                "the created account exists only to hold the admin role");
        });
    }

    /// <summary>
    /// The recommended path: the operator registers through the UI first, so no password ever
    /// enters the environment, and the next boot grants the role to that exact address.
    /// </summary>
    [Fact]
    public async Task With_email_only_an_existing_account_is_promoted()
    {
        await RunInTransactionAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var email = TestData.UniqueEmail("bootstrap-promote");
            var member = await CreateMemberAsync(userManager, email);

            await SeedAsync(userManager, Configuration(email));

            var reloaded = await userManager.FindByEmailAsync(email);
            Assert.NotNull(reloaded);
            Assert.Equal(member.Id, reloaded!.Id);
            Assert.True(
                await userManager.IsInRoleAsync(reloaded, Roles.Admin),
                "the account the operator registered must receive the admin role");
            Assert.True(
                await userManager.CheckPasswordAsync(reloaded, CreatePassword),
                "promotion must not change the password the operator already knows");
        });
    }

    /// <summary>
    /// Registration needs no mail confirmation, so anyone can claim the configured address
    /// first. When a password accompanies the address and it does not sign in to the existing
    /// account, the grant is refused: neither the role nor the password of the squatter's
    /// account may be touched.
    /// </summary>
    [Fact]
    public async Task With_a_mismatching_password_the_existing_account_is_not_promoted()
    {
        await RunInTransactionAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var email = TestData.UniqueEmail("bootstrap-refuse");
            await CreateMemberAsync(userManager, email);

            await SeedAsync(userManager, Configuration(email, "SomeOtherPass9!"));

            var reloaded = await userManager.FindByEmailAsync(email);
            Assert.NotNull(reloaded);
            Assert.False(
                await userManager.IsInRoleAsync(reloaded!, Roles.Admin),
                "a password that does not open the account proves it is not the operator's");
            Assert.True(
                await userManager.CheckPasswordAsync(reloaded, CreatePassword),
                "the existing account must keep the password it was registered with");
        });
    }

    /// <summary>
    /// The seeder runs on every boot, not once. A second pass over an account that already
    /// holds the role must find it there and leave it - and the user table - alone, so an
    /// operator who forgot to remove the variable gets no surprise on the next deploy either.
    /// </summary>
    [Fact]
    public async Task A_second_run_is_a_no_op()
    {
        await RunInTransactionAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = services.GetRequiredService<AppDbContext>();
            var email = TestData.UniqueEmail("bootstrap-repeat");
            const string password = "AdminPass1!";
            var configuration = Configuration(email, password);

            await SeedAsync(userManager, configuration);
            await SeedAsync(userManager, configuration);

            Assert.Equal(
                1,
                await dbContext.Users.CountAsync(user => user.NormalizedEmail == email.ToUpperInvariant()));
            var account = await userManager.FindByEmailAsync(email);
            Assert.NotNull(account);
            Assert.True(await userManager.IsInRoleAsync(account!, Roles.Admin));
            Assert.True(await userManager.CheckPasswordAsync(account, password));
        });
    }

    /// <summary>
    /// Every test writes accounts through the identity store, so the transaction is taken on
    /// the same scoped context the store saves through - one transaction that covers both the
    /// seeder's lookups and its writes, rolled back whatever the outcome.
    /// </summary>
    private async Task RunInTransactionAsync(Func<IServiceProvider, Task> body)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            await body(scope.ServiceProvider);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static async Task<ApplicationUser> CreateMemberAsync(
        UserManager<ApplicationUser> userManager,
        string email)
    {
        var member = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = "Ordinary Member",
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(member, CreatePassword);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));

        return member;
    }

    private static Task SeedAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration) =>
        BootstrapAdminSeeder.SeedAsync(userManager, configuration, NullLogger.Instance, CancellationToken.None);

    private static IConfiguration Configuration(string? email = null, string? password = null)
    {
        var values = new Dictionary<string, string?>();
        if (email is not null)
        {
            values[BootstrapAdminSeeder.EmailConfigurationKey] = email;
        }

        if (password is not null)
        {
            values[BootstrapAdminSeeder.PasswordConfigurationKey] = password;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
