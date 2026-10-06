using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Builder;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Tests;

public class NepalHubApiFactory : WebApplicationFactory<Program>
{
    public const string ValidPassword = "Passw0rd!";

private readonly int _authRateLimitPermitLimit;
    private readonly int _calculateRateLimitPermitLimit;
    private readonly int _quizRateLimitPermitLimit;
    private readonly int _searchRateLimitPermitLimit;
    private readonly int _reportRateLimitPermitLimit;
    private readonly int _logoutRateLimitPermitLimit;
    private readonly int _claimRateLimitPermitLimit;
    private readonly int _bookingRateLimitPermitLimit;
    private readonly int _paymentRateLimitPermitLimit;

    public NepalHubApiFactory()
        : this(10_000)
    {
    }

    protected NepalHubApiFactory(
        int authRateLimitPermitLimit,
        int calculateRateLimitPermitLimit = 10_000,
        int quizRateLimitPermitLimit = 10_000,
        int searchRateLimitPermitLimit = 10_000,
        int reportRateLimitPermitLimit = 10_000,
        int logoutRateLimitPermitLimit = 10_000,
        int claimRateLimitPermitLimit = 10_000,
        int bookingRateLimitPermitLimit = 10_000,
        int paymentRateLimitPermitLimit = 10_000)
    {
        _authRateLimitPermitLimit = authRateLimitPermitLimit;
        _calculateRateLimitPermitLimit = calculateRateLimitPermitLimit;
        _quizRateLimitPermitLimit = quizRateLimitPermitLimit;
        _searchRateLimitPermitLimit = searchRateLimitPermitLimit;
        _reportRateLimitPermitLimit = reportRateLimitPermitLimit;
        _logoutRateLimitPermitLimit = logoutRateLimitPermitLimit;
        _claimRateLimitPermitLimit = claimRateLimitPermitLimit;
        _bookingRateLimitPermitLimit = bookingRateLimitPermitLimit;
        _paymentRateLimitPermitLimit = paymentRateLimitPermitLimit;
        TestDatabase.PrepareAsync(
                _authRateLimitPermitLimit,
                _calculateRateLimitPermitLimit,
                _quizRateLimitPermitLimit,
                _searchRateLimitPermitLimit,
                _reportRateLimitPermitLimit,
                _logoutRateLimitPermitLimit,
                _claimRateLimitPermitLimit,
                _bookingRateLimitPermitLimit,
                _paymentRateLimitPermitLimit)
            .GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

    /// <summary>Registers a user and returns a client that is signed in as them.</summary>
    public HttpClient CreateSignedInClientAsync(string email)
    {
        var client = CreateClient();
        var api = new AuthApiClient(client);

        api.RegisterAsync(email, ValidPassword, "Test User").GetAwaiter().GetResult();
        api.LoginAsync(email, ValidPassword).GetAwaiter().GetResult();

        return client;
    }

    /// <summary>
    /// Grants a role to an existing account. The caller must sign in again
    /// afterwards, because the role claim is baked into the authentication cookie.
    /// </summary>
    public async Task GrantRoleAsync(string email, string role)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var result = await userManager.AddToRoleAsync(user!, role);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    /// <summary>
    /// Returns a client signed in as a fresh account holding <paramref name="role"/>.
    /// The role claim is baked into the cookie, so the account signs in again after
    /// the role is granted.
    /// </summary>
    public async Task<HttpClient> CreateStaffClientAsync(string role)
    {
        var email = TestData.UniqueEmail();
        using (var registered = CreateSignedInClientAsync(email))
        {
        }

        await GrantRoleAsync(email, role);

        var client = CreateClient();
        var api = new AuthApiClient(client);
        using var login = await api.LoginAsync(email, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return client;
    }
}

/// <summary>
/// A host whose auth endpoints reject almost immediately, so the rate limiter can
/// be asserted without waiting for a real window to elapse.
/// </summary>
public sealed class RateLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public RateLimitedApiFactory()
        : base(PermitLimit)
    {
    }
}

/// <summary>
/// A host whose calculation endpoint rejects almost immediately, for the same
/// reason as <see cref="RateLimitedApiFactory"/>.
/// </summary>
public sealed class CalculateLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public CalculateLimitedApiFactory()
        : base(10_000, PermitLimit)
    {
    }
}

/// <summary>
/// A host whose quiz submit endpoint rejects almost immediately, for the same
/// reason as <see cref="RateLimitedApiFactory"/>.
/// </summary>
public sealed class QuizLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public QuizLimitedApiFactory()
        : base(10_000, 10_000, PermitLimit)
    {
    }

    /// <summary>
    /// Lowers the quota on this host only. The application builds its configuration
    /// during startup, which happens before a test fixture constructor can affect it, so a
    /// process-wide environment variable set here would simply be ignored. The submit
    /// partitioner reads the limit from this holder on every lookup, so mutating the
    /// registered instance before the first request is enough, and it cannot leak into a
    /// host created by another test class.
    /// </summary>
    public void LowerSubmitQuota(int permitLimit) =>
        Services.GetRequiredService<QuizRateLimitOptions>().PermitLimit = permitLimit;
}

/// <summary>
/// A host whose search endpoint rejects almost immediately, for the same
/// reason as <see cref="RateLimitedApiFactory"/>.
/// </summary>
public sealed class SearchLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public SearchLimitedApiFactory()
        : base(10_000, 10_000, 10_000, PermitLimit)
    {
    }
}

/// <summary>
/// A host whose report endpoint rejects almost immediately, so the filing quota can be
/// asserted without a test filing hundreds of complaints.
/// </summary>
public sealed class ReportLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public ReportLimitedApiFactory()
        : base(10_000, 10_000, 10_000, 10_000, PermitLimit)
    {
    }
}

/// <summary>
/// A host whose sign-out endpoint rejects almost immediately, for the same reason as
/// <see cref="RateLimitedApiFactory"/>. The auth quota stays generous so the test can
/// still sign in between sign-outs if it needs to.
/// </summary>
public sealed class LogoutLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public LogoutLimitedApiFactory()
        : base(10_000, 10_000, 10_000, 10_000, 10_000, PermitLimit)
    {
    }
}

/// <summary>
/// A host whose claim endpoint rejects almost immediately, so the filing quota can be
/// asserted without a test claiming hundreds of listings.
/// </summary>
public sealed class ClaimLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public ClaimLimitedApiFactory()
        : base(10_000, 10_000, 10_000, 10_000, 10_000, 10_000, PermitLimit)
    {
    }
}

/// <summary>
/// A host whose booking endpoint rejects almost immediately, for the same reason: the quota
/// exists so a loop cannot take every place, and a test must not have to take them to see it.
/// </summary>
public sealed class BookingLimitedApiFactory : NepalHubApiFactory
{
    public const int PermitLimit = 3;

    public BookingLimitedApiFactory()
        : base(10_000, 10_000, 10_000, 10_000, 10_000, 10_000, 10_000, PermitLimit)
    {
    }
}

/// <summary>
/// A clock the tests can move. The daily quiz is chosen by the Nepal date, so pinning
/// "now" is the only way to exercise yesterday/today selection and streak arithmetic
/// without waiting for midnight.
/// </summary>
public sealed class MovableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public MovableTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow;
}

/// <summary>A host whose clock the test controls, exposed so the test can move it.</summary>
public sealed class FixedClockApiFactory : NepalHubApiFactory
{
    /// <summary>
    /// Builds a host whose clock is near the real one, so Identity still accepts a cookie
    /// issued moments ago. Identity validates the authentication ticket against the
    /// configured <c>TimeProvider</c>, and a clock pinned to a date in the past makes a
    /// freshly issued ticket look like it was issued in the future, which signs the
    /// player out. Tests then move the clock in whole days to change the Nepal date.
    /// </summary>
    public static FixedClockApiFactory NearNow() => new(DateTimeOffset.UtcNow);

    public FixedClockApiFactory(DateTimeOffset utcNow)
        : this(new MovableTimeProvider(utcNow))
    {
    }

    private FixedClockApiFactory(MovableTimeProvider clock)
        : base(10_000)
    {
        Clock = clock;
    }

    public MovableTimeProvider Clock { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

public sealed record ApiErrorPayload(string Code, string Message, JsonElement? Details);

public sealed record ApiEnvelope<T>(bool Success, T? Data, ApiErrorPayload? Error);

/// <summary>
/// Mirrors the API's own JSON settings, so a test can read any response the API
/// produces. The API writes enums by name, so a converter is required here too.
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}

/// <summary>Thin helper that keeps a cookie jar and always sends a fresh CSRF token.</summary>
public sealed class AuthApiClient(HttpClient client)
{
    public async Task<HttpResponseMessage> GetRawAsync(string path) =>
        await client.GetAsync(path);

    public async Task<T> GetAsync<T>(string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    public async Task<HttpResponseMessage> PostAsync(string path, object? body, bool withCsrf = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (withCsrf)
        {
            request.Headers.Add(AuthConstants.CsrfHeaderName, await GetCsrfTokenAsync());
        }

        return await client.SendAsync(request);
    }

    public async Task<string> GetCsrfTokenAsync()
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CsrfTokenResponse>>();
        Assert.NotNull(envelope);

        return envelope!.Data!.Token;
    }

    public async Task<HttpResponseMessage> RegisterAsync(string email, string password, string fullName)
    {
        return await PostAsync(
            "/api/auth/register",
            new { email, password, fullName, phoneNumber = (string?)null });
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        return await PostAsync("/api/auth/login", new { email, password, rememberMe = true });
    }

    public async Task<HttpResponseMessage> LogoutAsync() =>
        await PostAsync("/api/auth/logout", body: null);

    public async Task<ApiResponseSnapshot> PostForSnapshotAsync(string path, object? body)
    {
        using var response = await PostAsync(path, body);
        var json = await response.Content.ReadAsStringAsync();

        return new ApiResponseSnapshot(response.StatusCode, json);
    }

    public async Task<ApiResponseSnapshot> PutForSnapshotAsync(string path, object? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add(AuthConstants.CsrfHeaderName, await GetCsrfTokenAsync());
        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        return new ApiResponseSnapshot(response.StatusCode, json);
    }

    /// <summary>Sends a DELETE with a CSRF token, which the antiforgery middleware requires.</summary>
    public async Task<ApiResponseSnapshot> DeleteForSnapshotAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Add(AuthConstants.CsrfHeaderName, await GetCsrfTokenAsync());
        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        return new ApiResponseSnapshot(response.StatusCode, json);
    }
}

public sealed record ApiResponseSnapshot(HttpStatusCode StatusCode, string Json)
{
    private static readonly JsonSerializerOptions SerializerOptions = TestJson.Options;

    public ApiErrorPayload? Error
    {
        get
        {
            using var document = JsonDocument.Parse(Json);

            return document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                    ? error.Deserialize<ApiErrorPayload>(SerializerOptions)
                    : null;
        }
    }
}

public static class TestData
{
    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@nepalhub.test";
}
