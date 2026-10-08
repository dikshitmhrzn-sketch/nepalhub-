using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

/// <summary>
/// Recovery as it plays out when someone cannot sign in: a link is asked for, the answer to
/// the ask never says whether the account exists, the link sets a new password exactly once,
/// and a password the policy refuses does not burn the link on the way to refusing it.
/// </summary>
public class PasswordRecoveryTests
{
    [Fact]
    public async Task Forgot_password_answers_identically_whether_or_not_the_address_exists()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma");
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Single(host.Recorder.Messages);

        using var known = await api.PostAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        var knownBody = await known.Content.ReadAsStringAsync();
        Assert.Equal(2, host.Recorder.Messages.Count);
        Assert.Equal("reset-password", host.Recorder.Messages[1].Purpose);
        Assert.Equal("Reset your password for Nepal Hub", host.Recorder.Messages[1].Subject);

        using var unknown = await api.PostAsync(
            "/api/auth/forgot-password",
            new { email = TestData.UniqueEmail("stranger") });
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(knownBody, await unknown.Content.ReadAsStringAsync());
        Assert.Equal(2, host.Recorder.Messages.Count);
    }

    [Fact]
    public async Task The_link_sets_a_new_password_once_and_then_stops_working()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using (var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma"))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        var verification = EmailLinks.Read(host.Recorder.Messages[0]);
        using (var confirmed = await api.PostAsync("/api/auth/verify-email", new { verification.Email, verification.Token }))
        {
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        }

        using (var forgot = await api.PostAsync("/api/auth/forgot-password", new { email }))
        {
            Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        }

        var recovery = EmailLinks.Read(host.Recorder.Messages[1]);
        const string newPassword = "BrandNewPass1!";

        using var reset = await api.PostAsync(
            "/api/auth/reset-password",
            new { recovery.Email, recovery.Token, Password = newPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        using var oldPassword = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);

        using var signIn = await api.LoginAsync(email, newPassword);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        // The stamp Identity rotated on the way through makes every other copy of this
        // link a dead one — including a browser that kept the page open and clicks again.
        using var reused = await api.PostAsync(
            "/api/auth/reset-password",
            new { recovery.Email, recovery.Token, Password = "AnotherPass1!" });
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Contains("token_invalid", await reused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_bad_token_and_an_unknown_address_get_the_same_answer()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());

        using var badToken = await api.PostAsync(
            "/api/auth/reset-password",
            new { email = TestData.UniqueEmail(), token = "not-a-token-at-all", password = "SomeNewPass1!" });
        Assert.Equal(HttpStatusCode.BadRequest, badToken.StatusCode);
        var badTokenBody = await badToken.Content.ReadAsStringAsync();
        Assert.Contains("token_invalid", badTokenBody);

        using var unknownAddress = await api.PostAsync(
            "/api/auth/reset-password",
            new { email = TestData.UniqueEmail(), token = "not-a-token-at-all", password = "SomeNewPass1!" });
        Assert.Equal(HttpStatusCode.BadRequest, unknownAddress.StatusCode);
        Assert.Equal(badTokenBody, await unknownAddress.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_password_the_policy_refuses_leaves_the_link_usable()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using (var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Sharma"))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        var verification = EmailLinks.Read(host.Recorder.Messages[0]);
        using (var confirmed = await api.PostAsync("/api/auth/verify-email", new { verification.Email, verification.Token }))
        {
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        }

        using (var forgot = await api.PostAsync("/api/auth/forgot-password", new { email }))
        {
            Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        }

        var recovery = EmailLinks.Read(host.Recorder.Messages[1]);

        using var refused = await api.PostAsync(
            "/api/auth/reset-password",
            new { recovery.Email, recovery.Token, Password = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var refusedBody = await refused.Content.ReadAsStringAsync();
        Assert.Contains("validation_failed", refusedBody);
        Assert.Contains("password_too_short", refusedBody);

        // Identity judges the password only after the token passes and changes nothing
        // when it fails, so the person can come straight back with a password that passes.
        using var retried = await api.PostAsync(
            "/api/auth/reset-password",
            new { recovery.Email, recovery.Token, Password = "FinePass1!" });
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    /// <summary>
    /// A host with no provider configured — the default install — must still answer when
    /// somebody asks to resend or to recover, even though there is no origin to build a
    /// link from. There is simply nothing to send, and that is a log line, not a 500.
    /// </summary>
    [Fact]
    public async Task Asking_for_mail_a_host_cannot_build_still_gets_an_answer()
    {
        using var host = new NepalHubApiFactory();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using (var scope = host.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = await userManager.CreateAsync(
                new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    FullName = "Pending Person",
                    EmailConfirmed = false,
                },
                NepalHubApiFactory.ValidPassword);

            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        using var resend = await api.PostAsync("/api/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        using var forgot = await api.PostAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
    }
}
