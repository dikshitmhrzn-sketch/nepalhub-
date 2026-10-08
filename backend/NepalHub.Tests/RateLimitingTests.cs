using System.Net;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

public sealed class RateLimitingTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public RateLimitingTests(RateLimitedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Auth_endpoints_reject_once_the_window_is_exhausted()
    {
        using var client = _factory.CreateClient();
        var api = new AuthApiClient(client);

        for (var attempt = 0; attempt < RateLimitedApiFactory.PermitLimit; attempt++)
        {
            using var allowed = await api.LoginAsync(TestData.UniqueEmail(), "Wr0ngPassword!");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var limited = await api.PostForSnapshotAsync(
            "/api/auth/login",
            new { email = TestData.UniqueEmail(), password = "Wr0ngPassword!", rememberMe = false });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);
    }

    [Fact]
    public async Task Read_only_endpoints_are_not_rate_limited()
    {
        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < RateLimitedApiFactory.PermitLimit + 3; attempt++)
        {
            using var response = await client.GetAsync("/api/auth/session");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

/// <summary>
/// Filing a claim writes into the staff queue, so it is bounded per IP like filing a report:
/// a loop of claims is exactly the flood the queue exists to not be handed.
/// </summary>
public sealed class ClaimRateLimitingTests(ClaimLimitedApiFactory factory)
    : IClassFixture<ClaimLimitedApiFactory>
{
    [Fact]
    public async Task Filing_claims_stops_at_the_quota()
    {
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(TestData.UniqueEmail("claim-limit")));

        for (var attempt = 0; attempt < ClaimLimitedApiFactory.PermitLimit; attempt++)
        {
            var business = await OwnershipApi.CreateBusinessAsync(factory);
            using var allowed = await api.PostAsync(
                $"/api/businesses/{business.Slug}/claim",
                new { evidence = (string?)null });
            Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        }

        var extra = await OwnershipApi.CreateBusinessAsync(factory);
        var limited = await api.PostForSnapshotAsync(
            $"/api/businesses/{extra.Slug}/claim",
            new { evidence = (string?)null });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);
    }
}

/// <summary>
/// Booking takes a scarce seat, so it shares the claim's per-IP hour: three places in, the
/// fourth attempt is refused whatever the event's own capacity says.
/// </summary>
public sealed class BookingRateLimitingTests(BookingLimitedApiFactory factory)
    : IClassFixture<BookingLimitedApiFactory>
{
    [Fact]
    public async Task Booking_stops_at_the_quota()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(factory, capacity: 1_000);

        for (var attempt = 0; attempt < BookingLimitedApiFactory.PermitLimit; attempt++)
        {
            var attendee = factory.CreateSignedInClientAsync(TestData.UniqueEmail("seat-limit"));
            using var allowed = await new AuthApiClient(attendee).PostAsync(
                $"/api/events/{calendarEvent.Slug}/booking",
                new { quantity = 1 });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var extra = factory.CreateSignedInClientAsync(TestData.UniqueEmail("seat-limit"));
        var limited = await new AuthApiClient(extra).PostForSnapshotAsync(
            $"/api/events/{calendarEvent.Slug}/booking",
            new { quantity = 1 });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);
    }
}

/// <summary>
/// Recovery is bounded per IP for the same hour as filing a report: both halves — asking for
/// the link and spending it — draw on one quota, so a loop cannot switch endpoints for a
/// second attempt. The answer before the quota runs out never says whether the account
/// exists, and past it the answer is a refusal whatever the email.
/// </summary>
public sealed class PasswordResetRateLimitingTests(PasswordResetLimitedApiFactory factory)
    : IClassFixture<PasswordResetLimitedApiFactory>
{
    [Fact]
    public async Task Recovery_stops_at_the_quota()
    {
        var api = new AuthApiClient(factory.CreateClient());
        var email = TestData.UniqueEmail("reset-limit");

        for (var attempt = 0; attempt < PasswordResetLimitedApiFactory.PermitLimit; attempt++)
        {
            using var allowed = await api.PostAsync("/api/auth/forgot-password", new { email });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var limited = await api.PostForSnapshotAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);

        // Spending a link is the same spend from the same address book, so the hour is
        // shared: once asking is refused, spending is refused too.
        using var spentLimited = await api.PostAsync(
            "/api/auth/reset-password",
            new { email, token = "not-a-token-at-all", password = "SomeNewPass1!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, spentLimited.StatusCode);
        Assert.Contains("rate_limited", await spentLimited.Content.ReadAsStringAsync());
    }
}

/// <summary>
/// Resending a confirmation link shares the reset endpoint's hour and quota: the legitimate
/// caller lost one email, while a loop is using the endpoint to make this host write to
/// somebody else's inbox at volume.
/// </summary>
public sealed class ResendVerificationRateLimitingTests(ResendVerificationLimitedApiFactory factory)
    : IClassFixture<ResendVerificationLimitedApiFactory>
{
    [Fact]
    public async Task Resending_stops_at_the_quota()
    {
        var api = new AuthApiClient(factory.CreateClient());
        var email = TestData.UniqueEmail("resend-limit");

        for (var attempt = 0; attempt < ResendVerificationLimitedApiFactory.PermitLimit; attempt++)
        {
            using var allowed = await api.PostAsync("/api/auth/resend-verification", new { email });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var limited = await api.PostForSnapshotAsync("/api/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);
    }
}

/// <summary>
/// Changing a password requires the current one, so the quota is for the caller grinding at
/// it: the wrong guesses count, and the loop worth stopping is refused whichever password
/// it offers. A handful of attempts in an hour is a person who keeps fat-fingering.
/// </summary>
public sealed class ChangePasswordRateLimitingTests(ChangePasswordLimitedApiFactory factory)
    : IClassFixture<ChangePasswordLimitedApiFactory>
{
    [Fact]
    public async Task Changing_password_stops_at_the_quota()
    {
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("password-limit")));

        for (var attempt = 0; attempt < ChangePasswordLimitedApiFactory.PermitLimit; attempt++)
        {
            using var refused = await api.PostAsync(
                "/api/account/change-password",
                new { currentPassword = "Wr0ngPassword!", newPassword = "BrandNewPass1!" });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        var limited = await api.PostForSnapshotAsync(
            "/api/account/change-password",
            new { currentPassword = "Wr0ngPassword!", newPassword = "BrandNewPass1!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);
    }
}

/// <summary>
/// Registering a listing opens a row in the staff queue, so it is bounded per IP like filing
/// a claim: a loop of registrations is exactly the flood the queue exists to not be handed,
/// and asking for verification is the same spend a moment later.
/// </summary>
public sealed class BusinessRateLimitingTests(BusinessLimitedApiFactory factory)
    : IClassFixture<BusinessLimitedApiFactory>
{
    [Fact]
    public async Task Registering_stops_at_the_quota()
    {
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-limit")));

        for (var attempt = 0; attempt < BusinessLimitedApiFactory.PermitLimit; attempt++)
        {
            using var allowed = await api.PostAsync(
                RegistrationApi.AccountPath,
                RegistrationApi.RegisterBody(BusinessApi.NewSlug()));
            Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        }

        var limited = await api.PostForSnapshotAsync(
            RegistrationApi.AccountPath,
            RegistrationApi.RegisterBody(BusinessApi.NewSlug()));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", limited.Error!.Code);

        // The verification request shares that quota: both spend the same per-IP hour, so
        // once registration is refused, asking for the check it opened is refused too.
        var slug = BusinessApi.NewSlug();
        using var allowedRegistration = await api.PostAsync(
            RegistrationApi.AccountPath,
            RegistrationApi.RegisterBody(slug));
        Assert.Equal(HttpStatusCode.TooManyRequests, allowedRegistration.StatusCode);

        var refusedAsk = await api.PostForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{slug}/verification",
            new { note = (string?)null });
        Assert.Equal(HttpStatusCode.TooManyRequests, refusedAsk.StatusCode);
        Assert.Equal("rate_limited", refusedAsk.Error!.Code);
    }
}
