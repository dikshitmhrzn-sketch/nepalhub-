using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

public class NepalHubApiFactory : WebApplicationFactory<Program>
{
    public const string ValidPassword = "Passw0rd!";

    private readonly int _authRateLimitPermitLimit;

    public NepalHubApiFactory()
        : this(10_000)
    {
    }

    protected NepalHubApiFactory(int authRateLimitPermitLimit)
    {
        _authRateLimitPermitLimit = authRateLimitPermitLimit;
        TestDatabase.PrepareAsync(_authRateLimitPermitLimit).GetAwaiter().GetResult();
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

public sealed record ApiErrorPayload(string Code, string Message, JsonElement? Details);

public sealed record ApiEnvelope<T>(bool Success, T? Data, ApiErrorPayload? Error);

/// <summary>Thin helper that keeps a cookie jar and always sends a fresh CSRF token.</summary>
public sealed class AuthApiClient(HttpClient client)
{
    public async Task<HttpResponseMessage> GetRawAsync(string path) =>
        await client.GetAsync(path);

    public async Task<T> GetAsync<T>(string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>();
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
}

public sealed record ApiResponseSnapshot(HttpStatusCode StatusCode, string Json)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
