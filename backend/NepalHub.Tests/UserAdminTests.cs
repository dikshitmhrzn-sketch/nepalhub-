using System.Net;
using System.Net.Http.Json;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

/// <summary>The JSON shape the admin account list returns, plus the reads the tests share.</summary>
internal static class UserAdminViews
{
    internal sealed record UserRowView(
        Guid Id,
        string Email,
        string FullName,
        int Level,
        int Xp,
        bool IsActive,
        IReadOnlyList<string> Roles,
        DateTime CreatedAt);

    internal static async Task<Moderations.PageView<UserRowView>> ReadListAsync(
        HttpClient client, string query = "")
    {
        using var response = await client.GetAsync($"/api/admin/users{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<Moderations.PageView<UserRowView>>>(TestJson.Options);

        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    /// <summary>The account row a successful activate, deactivate or role change answers with.</summary>
    internal static async Task<UserRowView> ReadChangedAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<UserRowView>>(TestJson.Options);

        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    /// <summary>The row for one account, found by the address only this test knows.</summary>
    internal static async Task<UserRowView> FindAccountAsync(HttpClient client, string email) =>
        (await ReadListAsync(client, $"?search={email}")).Items.Single();
}

/// <summary>
/// The administrator's account screen. Every test signs in of its own accord: the point of
/// each one is who may reach the endpoint and what it does to an account, so there is no
/// shared client for the assertions to lean on.
/// </summary>
public sealed class UserAdminTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string Path = "/api/admin/users";

    [Fact]
    public async Task The_account_screen_is_admin_only()
    {
        using var anonymous = factory.CreateClient();
        using (var refused = await anonymous.GetAsync(Path))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        using var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("plain"));
        using (var refused = await member.GetAsync(Path))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The moderation queue is staff; this screen is not, and the difference between
        // the two policies is exactly the line this assertion draws.
        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        using (var refused = await moderator.GetAsync(Path))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        using (var allowed = await admin.GetAsync(Path))
        {
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }
    }

    [Fact]
    public async Task The_list_shows_each_account_with_its_roles_and_narrows_on_request()
    {
        var email = TestData.UniqueEmail("listed");
        using var account = factory.CreateSignedInClientAsync(email);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);

        var row = await UserAdminViews.FindAccountAsync(admin, email);
        Assert.Equal(email, row.Email);
        Assert.True(row.IsActive);
        Assert.Contains("user", row.Roles);

        var inactive = await UserAdminViews.ReadListAsync(
            admin, $"?search={email}&status=inactive");
        Assert.Empty(inactive.Items);

        var active = await UserAdminViews.ReadListAsync(
            admin, $"?search={email}&status=active");
        Assert.Single(active.Items);

        var byRole = await UserAdminViews.ReadListAsync(
            admin, $"?search={email}&role=user");
        Assert.Single(byRole.Items);

        using var badStatus = await admin.GetAsync($"{Path}?status=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
        Assert.Contains("status_invalid", await badStatus.Content.ReadAsStringAsync());

        using var badRole = await admin.GetAsync($"{Path}?role=nonsense");
        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
        Assert.Contains("role_invalid", await badRole.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Disabling_and_re_enabling_an_account_reaches_the_login_screen_and_the_log()
    {
        var email = TestData.UniqueEmail("disabled");
        using var account = factory.CreateSignedInClientAsync(email);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(admin);

        var target = await UserAdminViews.FindAccountAsync(admin, email);

        using var turnedOff = await adminApi.PostAsync($"{Path}/{target.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, turnedOff.StatusCode);
        Assert.False((await UserAdminViews.ReadChangedAsync(turnedOff)).IsActive);

        // The switch is the whole point: a disabled account cannot get back in.
        using var visitor = factory.CreateClient();
        var visitorApi = new AuthApiClient(visitor);
        using var denied = await visitorApi.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("account_disabled", await denied.Content.ReadAsStringAsync());

        var nowInactive = await UserAdminViews.ReadListAsync(
            admin, $"?search={email}&status=inactive");
        Assert.Single(nowInactive.Items);

        using var turnedOn = await adminApi.PostAsync($"{Path}/{target.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, turnedOn.StatusCode);
        Assert.True((await UserAdminViews.ReadChangedAsync(turnedOn)).IsActive);

        using var allowed = await visitorApi.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        var deactivations = await Moderations.ReadAuditAsync(
            admin, "?action=user.deactivated&targetType=user&pageSize=100");
        var turned = Assert.Single(
            deactivations.Data!.Items, entry => entry.TargetId == target.Id.ToString());
        Assert.Contains("\"isActive\":true", turned.Before);
        Assert.Contains("\"isActive\":false", turned.After);

        var activations = await Moderations.ReadAuditAsync(
            admin, "?action=user.activated&targetType=user&pageSize=100");
        Assert.Single(activations.Data!.Items, entry => entry.TargetId == target.Id.ToString());
    }

    [Fact]
    public async Task An_administrator_cannot_turn_off_or_re_role_their_own_account()
    {
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(admin);

        var session = await api.GetAsync<SessionResponse>("/api/auth/session");
        var ownId = session.User!.Id;

        using var turnedOff = await api.PostAsync($"{Path}/{ownId}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, turnedOff.StatusCode);
        Assert.Equal("cannot_modify_self", (await turnedOff.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Error!.Code);

        using var demoted = await api.PostAsync(
            $"{Path}/{ownId}/roles/revoke", new { role = "admin" });
        Assert.Equal(HttpStatusCode.Conflict, demoted.StatusCode);

        using var promoted = await api.PostAsync(
            $"{Path}/{ownId}/roles/grant", new { role = "admin" });
        Assert.Equal(HttpStatusCode.Conflict, promoted.StatusCode);

        // The refusal kept the account whole: it still reaches the screen it started on.
        using var stillAdmin = await admin.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, stillAdmin.StatusCode);
    }

    [Fact]
    public async Task A_role_can_be_handed_out_and_taken_back_and_the_account_sees_it_after_signing_in_again()
    {
        var email = TestData.UniqueEmail("promoted");
        using var registration = factory.CreateSignedInClientAsync(email);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(admin);

        var target = await UserAdminViews.FindAccountAsync(admin, email);

        using var granted = await adminApi.PostAsync(
            $"{Path}/{target.Id}/roles/grant", new { role = "moderator" });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        var grantedBody = await UserAdminViews.ReadChangedAsync(granted);
        Assert.Contains("moderator", grantedBody.Roles);
        Assert.Contains("user", grantedBody.Roles);

        using var grantedTwice = await adminApi.PostAsync(
            $"{Path}/{target.Id}/roles/grant", new { role = "moderator" });
        Assert.Equal(HttpStatusCode.Conflict, grantedTwice.StatusCode);
        Assert.Equal("role_already_assigned", (await grantedTwice.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Error!.Code);

        // The old cookie still says "user"; a fresh sign-in is where the change lands.
        var renewed = factory.CreateClient();
        var renewedApi = new AuthApiClient(renewed);
        using var signIn = await renewedApi.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var afterGrant = await renewedApi.GetAsync<SessionResponse>("/api/auth/session");
        Assert.Contains("moderator", afterGrant.User!.Roles);

        var trail = await Moderations.ReadAuditAsync(
            admin, "?action=user.role_granted&targetType=user&pageSize=100");
        var entry = Assert.Single(trail.Data!.Items, row => row.TargetId == target.Id.ToString());
        Assert.Contains("moderator", entry.After);
        Assert.DoesNotContain("moderator", entry.Before);

        using var revoked = await adminApi.PostAsync(
            $"{Path}/{target.Id}/roles/revoke", new { role = "moderator" });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.DoesNotContain("moderator", (await UserAdminViews.ReadChangedAsync(revoked)).Roles);

        using var revokedTwice = await adminApi.PostAsync(
            $"{Path}/{target.Id}/roles/revoke", new { role = "moderator" });
        Assert.Equal(HttpStatusCode.Conflict, revokedTwice.StatusCode);
        Assert.Equal("role_not_assigned", (await revokedTwice.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Error!.Code);

        var fresh = factory.CreateClient();
        var freshApi = new AuthApiClient(fresh);
        using var renewedSignIn = await freshApi.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, renewedSignIn.StatusCode);
        var afterRevoke = await freshApi.GetAsync<SessionResponse>("/api/auth/session");
        Assert.DoesNotContain("moderator", afterRevoke.User!.Roles);
    }

    [Fact]
    public async Task A_role_the_platform_does_not_have_is_refused()
    {
        var email = TestData.UniqueEmail("roled");
        using var registration = factory.CreateSignedInClientAsync(email);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(admin);

        var target = await UserAdminViews.FindAccountAsync(admin, email);

        using var granted = await api.PostAsync(
            $"{Path}/{target.Id}/roles/grant", new { role = "wizard" });
        Assert.Equal(HttpStatusCode.BadRequest, granted.StatusCode);

        var envelope = await granted.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("role_invalid", envelope!.Error!.Code);
        Assert.Contains("role", envelope.Error.Details?.ToString());
    }

    [Fact]
    public async Task An_account_that_does_not_exist_is_answered_with_absence()
    {
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(admin);

        var missing = Guid.NewGuid();

        using var turnedOff = await api.PostAsync($"{Path}/{missing}/deactivate", null);
        Assert.Equal(HttpStatusCode.NotFound, turnedOff.StatusCode);
        Assert.Equal("user_not_found", (await turnedOff.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Error!.Code);

        using var granted = await api.PostAsync(
            $"{Path}/{missing}/roles/grant", new { role = "moderator" });
        Assert.Equal(HttpStatusCode.NotFound, granted.StatusCode);
        Assert.Equal("user_not_found", (await granted.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Error!.Code);
    }
}
