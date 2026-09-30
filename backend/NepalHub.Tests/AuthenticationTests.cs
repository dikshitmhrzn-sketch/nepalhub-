using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

public sealed class AuthenticationTests : IClassFixture<NepalHubApiFactory>
{
    private readonly NepalHubApiFactory _factory;

    public AuthenticationTests(NepalHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_creates_the_account_and_signs_the_user_in()
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);
        var email = TestData.UniqueEmail();

        using var response = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Sita Kumari");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var session = await api.GetAsync<SessionResponse>("/api/auth/session");
        Assert.True(session.Authenticated);
        Assert.NotNull(session.User);
        Assert.Equal(email, session.User!.Email);
        Assert.Equal("Sita Kumari", session.User.FullName);
        Assert.Contains(Roles.User, session.User.Roles);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_with_a_field_error()
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);
        var email = TestData.UniqueEmail();

        using (var first = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "First"))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        var second = await api.PostForSnapshotAsync(
            "/api/auth/register",
            new { email, password = NepalHubApiFactory.ValidPassword, fullName = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("validation_failed", second.Error!.Code);

        var details = second.Error.Details!.Value.Deserialize<FieldError[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var emailError = Assert.Single(details!);
        Assert.Equal("email", emailError.Field);
        Assert.Equal("email_taken", emailError.Code);
    }

    [Theory]
    [InlineData("Shrt1!A", "password_too_short")]
    [InlineData("alllowercase1!", "password_too_weak")]
    [InlineData("ALLUPPERCASE1!", "password_too_weak")]
    [InlineData("NoDigitsHere!", "password_too_weak")]
    [InlineData("NoSymbols1234", "password_too_weak")]
    public async Task Register_rejects_weak_passwords(string password, string expectedCode)
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);

        var result = await api.PostForSnapshotAsync(
            "/api/auth/register",
            new { email = TestData.UniqueEmail(), password, fullName = "Weak Password" });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);

        var details = result.Error!.Details!.Value.Deserialize<FieldError[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var passwordError = Assert.Single(details!);
        Assert.Equal("password", passwordError.Field);
        Assert.Equal(expectedCode, passwordError.Code);
    }

    [Fact]
    public async Task Register_rejects_a_request_without_a_csrf_token()
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);

        var result = await api.PostForSnapshotAsync(
            "/api/auth/register",
            new
            {
                email = TestData.UniqueEmail(),
                password = NepalHubApiFactory.ValidPassword,
                fullName = "No Csrf",
            });

        // PostForSnapshotAsync always sends a token, so send a raw request instead.
        Assert.NotEqual(HttpStatusCode.BadRequest, result.StatusCode);

        using var raw = await client.PostAsync(
            "/api/auth/register",
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    email = TestData.UniqueEmail(),
                    password = NepalHubApiFactory.ValidPassword,
                    fullName = "No Csrf",
                }),
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, raw.StatusCode);
        var error = await raw.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        Assert.Equal("csrf_failed", error!.Error!.Code);
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password_without_revealing_the_account()
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);
        var email = TestData.UniqueEmail();

        using (var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Wrong Password"))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        await api.LogoutAsync();
        var result = await api.PostForSnapshotAsync(
            "/api/auth/login",
            new { email, password = "Wr0ngPassword!", rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal("invalid_credentials", result.Error!.Code);

        var session = await api.GetAsync<SessionResponse>("/api/auth/session");
        Assert.False(session.Authenticated);
    }

    [Fact]
    public async Task Login_with_the_right_password_restores_the_session()
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);
        var email = TestData.UniqueEmail();

        using (var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Login User"))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        await api.LogoutAsync();
        using var login = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await api.GetAsync<SessionUserResponse>("/api/account/me");
        Assert.Equal(email, me.Email);
    }

    [Fact]
    public async Task Logout_clears_the_session()
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);

        using (var registered = await api.RegisterAsync(
            TestData.UniqueEmail(),
            NepalHubApiFactory.ValidPassword,
            "Logout User"))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        var before = await api.GetAsync<SessionResponse>("/api/auth/session");
        Assert.True(before.Authenticated);

        using var logout = await api.LogoutAsync();
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var after = await api.GetAsync<SessionResponse>("/api/auth/session");
        Assert.False(after.Authenticated);
        Assert.Null(after.User);

        using var denied = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Account_requires_a_signed_in_user()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/account/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_standard_user_is_not_an_administrator()
    {
        var email = TestData.UniqueEmail();
        using var client = _factory.CreateSignedInClientAsync(email);

        using var response = await client.GetAsync("/api/admin/ping");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_reaches_the_admin_endpoint()
    {
        var email = TestData.UniqueEmail();
        using var seeded = _factory.CreateSignedInClientAsync(email);
        await _factory.GrantRoleAsync(email, Roles.Admin);

        // The role claim only reaches the client on a new sign-in.
        using var client = _factory.CreateClient();
        var api = new AuthApiClient(client);
        using var login = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var session = await api.GetAsync<SessionResponse>("/api/auth/session");
        Assert.Contains(Roles.Admin, session.User!.Roles);

        using var response = await client.GetAsync("/api/admin/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account()
    {
        var client = _factory.CreateClient();
        var api = new AuthApiClient(client);
        var email = TestData.UniqueEmail();

        using (var registered = await api.RegisterAsync(email, NepalHubApiFactory.ValidPassword, "Lockout User"))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        await api.LogoutAsync();

        // Identity locks the account once the fifth failure is recorded, so only
        // the first four attempts are answered with a plain rejection.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var failed = await api.LoginAsync(email, "Wr0ngPassword!");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await api.PostForSnapshotAsync(
            "/api/auth/login",
            new { email, password = "Wr0ngPassword!", rememberMe = false });

        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        Assert.Equal("account_locked", locked.Error!.Code);

        // Even the correct password is refused while the lockout is active.
        var correct = await api.PostForSnapshotAsync(
            "/api/auth/login",
            new { email, password = NepalHubApiFactory.ValidPassword, rememberMe = false });

        Assert.Equal((HttpStatusCode)423, correct.StatusCode);
    }
}

public sealed record FieldError(string Field, string Code, string Message);
