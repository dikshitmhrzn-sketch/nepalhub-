using System.Net;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

/// <summary>
/// Changing a password is the account's own door: the current one is required, the old
/// password stops working the moment the new one takes, a wrong current password is
/// refused with the same shape every other password failure answers with, and a person
/// nobody can authenticate cannot change anything at all.
/// </summary>
public sealed class ChangePasswordTests
{
    [Fact]
    public async Task Changing_the_password_signs_the_old_one_out_and_says_so_by_mail()
    {
        using var host = MailApiFactory.Create();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using (var registered = await api.RegisterAsync(
            email, NepalHubApiFactory.ValidPassword, "Sita Sharma"))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        var verification = EmailLinks.Read(host.Recorder.Messages[0]);
        using (var confirmed = await api.PostAsync(
            "/api/auth/verify-email",
            new { verification.Email, verification.Token }))
        {
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        }

        using (var signIn = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword))
        {
            Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        }

        const string newPassword = "BrandNewPass1!";
        using var changed = await api.PostAsync(
            "/api/account/change-password",
            new { currentPassword = NepalHubApiFactory.ValidPassword, newPassword });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // Identity rotated the security stamp on the way through, so the old password
        // stops working wherever it was known, and the notice is the courtesy that says
        // somebody moved the door.
        using var oldPassword = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);

        using var signInWithNew = await api.LoginAsync(email, newPassword);
        Assert.Equal(HttpStatusCode.OK, signInWithNew.StatusCode);

        Assert.Equal(2, host.Recorder.Messages.Count);
        var notice = host.Recorder.Messages[1];
        Assert.Equal("password-changed", notice.Purpose);
        Assert.Equal(email, notice.To);
    }

    [Fact]
    public async Task A_wrong_current_password_is_refused_and_changes_nothing()
    {
        // Provider none: registration signs the person straight in, so the fixture host is
        // enough — the refusal never needs the mail half of the module to answer.
        using var host = new NepalHubApiFactory();
        var api = new AuthApiClient(host.CreateClient());
        var email = TestData.UniqueEmail();

        using var registered = await api.RegisterAsync(
            email, NepalHubApiFactory.ValidPassword, "Sita Sharma");
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var refused = await api.PostAsync(
            "/api/account/change-password",
            new { currentPassword = "Wr0ngPassword!", newPassword = "BrandNewPass1!" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var body = await refused.Content.ReadAsStringAsync();
        Assert.Contains("validation_failed", body);
        Assert.Contains("password_incorrect", body);

        // Nothing moved: the password the person already had still opens the door.
        using var signIn = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task Someone_not_signed_in_cannot_change_anything()
    {
        using var host = new NepalHubApiFactory();
        var api = new AuthApiClient(host.CreateClient());

        using var refused = await api.PostAsync(
            "/api/account/change-password",
            new { currentPassword = "Whatever1!", newPassword = "BrandNewPass1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }
}
