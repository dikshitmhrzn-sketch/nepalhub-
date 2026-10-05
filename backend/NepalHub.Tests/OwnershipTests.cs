using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Businesses;
using NepalHub.Api.Features.Ownership;

namespace NepalHub.Tests;

/// <summary>The JSON shapes the ownership endpoints return, read back with the shared test options.</summary>
internal static class OwnershipViews
{
    internal sealed record OwnerFlagView(Guid Id, string Slug, bool IsOwner);

    internal sealed record OwnerBusinessView(
        Guid Id,
        string Slug,
        string Name,
        string Description,
        bool IsPublished,
        bool IsVerified);

    internal sealed record MyBusinessesView(
        IReadOnlyList<MyOwnedView> Owned,
        IReadOnlyList<MyClaimView> Claims);

    internal sealed record MyOwnedView(
        Guid Id,
        string Slug,
        string Name,
        BusinessCategory Category,
        string DistrictName,
        bool IsPublished,
        bool IsVerified);

    internal sealed record MyClaimView(
        Guid Id,
        Guid BusinessId,
        string Slug,
        string BusinessName,
        BusinessClaimStatus Status,
        string? Evidence,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewNote);
}

/// <summary>
/// Shared moves for the claim tests: a listing to aim at, the filing itself, and the whole
/// claim-to-owner path the subscription tests also stand on.
/// </summary>
internal static class OwnershipApi
{
    internal const string ClaimAdminPath = "/api/admin/business-claims";

    /// <summary>
    /// A listing staff have just made, in any publish and verify state. The name carries the
    /// slug's unique token so the row can be found again if a test needs to.
    /// </summary>
    internal static async Task<BusinessAdminResponse> CreateBusinessAsync(
        NepalHubApiFactory factory,
        bool published = true,
        bool isVerified = true)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        // A published listing needs a phone number: the publish rules live on the save path,
        // so a fixture that skips the phone is refused rather than published silently.
        var created = await api.PostForSnapshotAsync(
            "/api/admin/businesses",
            BusinessApi.Body(
                slug,
                $"Claim Target {slug}",
                published,
                phone: "9800000301",
                isVerified: isVerified));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return BusinessApi.ReadCreated(created);
    }

    internal static async Task<ClaimResponse> ClaimAsync(
        AuthApiClient claimant,
        string slug,
        string? evidence = null)
    {
        using var response = await claimant.PostAsync(
            $"/api/businesses/{slug}/claim", new { evidence });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<ClaimResponse>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    internal static async Task<Moderations.PageView<AdminClaimSummaryResponse>> ReadQueueAsync(
        HttpClient client,
        string query = "")
    {
        using var response = await client.GetAsync($"{ClaimAdminPath}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<Moderations.PageView<AdminClaimSummaryResponse>>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    /// <summary>
    /// Signs an existing account in again on a fresh client. The role claim is baked into the
    /// cookie, so anything that changed a role since the original sign-in is only visible here.
    /// </summary>
    internal static async Task<HttpClient> SignInAsync(NepalHubApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        using var login = await api.LoginAsync(email, NepalHubApiFactory.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return client;
    }

    internal static async Task<Guid> UserIdAsync(NepalHubApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        return user!.Id;
    }

    /// <summary>
    /// The whole path to an owner: staff publish a listing, a fresh member claims it, staff
    /// approve. This is the product's own route to ownership rather than a row written behind
    /// the API, so everything downstream — the role, the owner screen, the entitlement paths —
    /// is exercised against what a real approval leaves in the database.
    /// </summary>
    internal static async Task<(string Email, Guid BusinessId, string Slug)> MakeOwnerAsync(
        NepalHubApiFactory factory)
    {
        var business = await CreateBusinessAsync(factory);
        var email = TestData.UniqueEmail("owner");
        var client = factory.CreateSignedInClientAsync(email);
        var claim = await ClaimAsync(new AuthApiClient(client), business.Slug);

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var approved = await new AuthApiClient(staff).PostForSnapshotAsync(
            $"{ClaimAdminPath}/{claim.Id}/approve",
            new { reviewNote = (string?)null });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        return (email, business.Id, business.Slug);
    }
}

/// <summary>
/// Filing a claim as a member. Every test signs in of its own accord, because a claim without
/// an account behind it is the one thing this endpoint refuses outright.
/// </summary>
public sealed class ClaimFilingTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task Filing_a_claim_requires_an_account()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);
        var business = await OwnershipApi.CreateBusinessAsync(factory);

        using var response = await api.PostAsync(
            $"/api/businesses/{business.Slug}/claim",
            new { evidence = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_claim_reaches_the_queue_and_a_second_attempt_on_the_same_listing_is_refused()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        var email = TestData.UniqueEmail("claimant");
        var client = factory.CreateSignedInClientAsync(email);
        var api = new AuthApiClient(client);

        var claim = await OwnershipApi.ClaimAsync(api, business.Slug);
        Assert.Equal(BusinessClaimStatus.Pending, claim.Status);
        Assert.Equal(business.Slug, claim.Slug);

        var again = await api.PostForSnapshotAsync(
            $"/api/businesses/{business.Slug}/claim",
            new { evidence = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_claimed", again.Error!.Code);

        // Staff read the queue; a member of the site cannot.
        using var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("stranger"));
        using var refused = await member.GetAsync(OwnershipApi.ClaimAdminPath);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var queue = await OwnershipApi.ReadQueueAsync(moderator, "?pageSize=100");

        var row = Assert.Single(queue.Items, item => item.Id == claim.Id);
        Assert.Equal(business.Slug, row.Slug);
        Assert.Equal(business.Name, row.BusinessName);
        Assert.Equal(email, row.ClaimantEmail);
        Assert.Equal(BusinessClaimStatus.Pending, row.Status);

        // A typo in the filter is refused rather than quietly returning the whole table.
        using var nonsense = await moderator.GetAsync($"{OwnershipApi.ClaimAdminPath}?status=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
        Assert.Contains("status_invalid", await nonsense.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_draft_listing_answers_the_same_404_as_one_that_does_not_exist()
    {
        var draft = await OwnershipApi.CreateBusinessAsync(factory, published: false);
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("claimant"));
        var api = new AuthApiClient(client);

        var invisible = await api.PostForSnapshotAsync(
            $"/api/businesses/{draft.Slug}/claim",
            new { evidence = (string?)null });
        var missing = await api.PostForSnapshotAsync(
            $"/api/businesses/{BusinessApi.NewSlug()}/claim",
            new { evidence = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
        Assert.Equal("business_not_found", invisible.Error!.Code);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("business_not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task Evidence_longer_than_five_hundred_characters_is_refused()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("claimant"));
        var api = new AuthApiClient(client);

        var response = await api.PostForSnapshotAsync(
            $"/api/businesses/{business.Slug}/claim",
            new { evidence = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", response.Error!.Code);
        Assert.Contains("evidence", response.Error.Details?.ToString() ?? string.Empty);
    }
}

/// <summary>
/// The staff queue's decisions. Approval is the moment ownership, the business role, the rival
/// settlements and the audit line are all written at once, so each is asserted after one
/// approve call rather than across a sequence of repairs.
/// </summary>
public sealed class ClaimDecisionTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task Approving_moves_ownership_grants_the_role_and_leaves_one_audit_line()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);

        var winnerEmail = TestData.UniqueEmail("winner");
        var rivalEmail = TestData.UniqueEmail("rival");
        var winnerClient = factory.CreateSignedInClientAsync(winnerEmail);
        var rivalClient = factory.CreateSignedInClientAsync(rivalEmail);

        var winnerClaim = await OwnershipApi.ClaimAsync(new AuthApiClient(winnerClient), business.Slug);
        var rivalClaim = await OwnershipApi.ClaimAsync(new AuthApiClient(rivalClient), business.Slug);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var approved = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{OwnershipApi.ClaimAdminPath}/{winnerClaim.Id}/approve",
            new { reviewNote = "Proof supplied." });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbContext.Businesses.AsNoTracking()
            .SingleAsync(item => item.Id == business.Id);
        Assert.Equal(await OwnershipApi.UserIdAsync(factory, winnerEmail), stored.OwnerUserId);

        // Once one claim wins, the others are asking for something that can no longer be
        // granted, so the queue settles them in the same pass rather than leaving them open.
        var rival = await dbContext.BusinessClaims.AsNoTracking()
            .SingleAsync(item => item.Id == rivalClaim.Id);
        Assert.Equal(BusinessClaimStatus.Rejected, rival.Status);
        Assert.Equal("Another claim on this listing was approved.", rival.ReviewNote);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var winner = await userManager.FindByEmailAsync(winnerEmail);
        Assert.NotNull(winner);
        Assert.Contains(Roles.Business, await userManager.GetRolesAsync(winner!));

        // The role claim is in the cookie, so the winner signs in again to see it.
        using var reSigned = await OwnershipApi.SignInAsync(factory, winnerEmail);
        var winnerApi = new AuthApiClient(reSigned);
        var session = await winnerApi.GetAsync<SessionResponse>("/api/auth/session");
        Assert.Contains(Roles.Business, session.User!.Roles);

        var mine = await winnerApi.GetAsync<OwnershipViews.MyBusinessesView>("/api/account/businesses");
        Assert.Contains(mine.Owned, item => item.Slug == business.Slug);
        Assert.Contains(
            mine.Claims,
            item => item.Id == winnerClaim.Id && item.Status == BusinessClaimStatus.Approved);

        var detail = await winnerApi.GetAsync<OwnershipViews.OwnerFlagView>(
            $"/api/businesses/{business.Slug}");
        Assert.True(detail.IsOwner);

        // A moderator performed the decision, but only an admin may read the log.
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var audit = await Moderations.ReadAuditAsync(
            admin, "?action=business.claim_approved&pageSize=100");

        var entry = Assert.Single(
            audit.Data!.Items,
            item => item.TargetId == winnerClaim.Id.ToString());
        Assert.Equal("business_claim", entry.TargetType);
        Assert.Contains("Approved", entry.After);

        // The note lives on the claim itself; the log line records what changed.
        var decided = await dbContext.BusinessClaims.AsNoTracking()
            .SingleAsync(item => item.Id == winnerClaim.Id);
        Assert.Equal("Proof supplied.", decided.ReviewNote);
        Assert.NotNull(decided.ReviewedAt);
    }

    [Fact]
    public async Task A_settled_claim_cannot_be_decided_a_second_time()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("claimant"));
        var claim = await OwnershipApi.ClaimAsync(new AuthApiClient(client), business.Slug);

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var first = await api.PostForSnapshotAsync(
            $"{OwnershipApi.ClaimAdminPath}/{claim.Id}/approve", new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await api.PostForSnapshotAsync(
            $"{OwnershipApi.ClaimAdminPath}/{claim.Id}/approve", new { reviewNote = (string?)null });
        var rejected = await api.PostForSnapshotAsync(
            $"{OwnershipApi.ClaimAdminPath}/{claim.Id}/reject", new { reviewNote = (string?)null });
        var unknown = await api.PostForSnapshotAsync(
            $"{OwnershipApi.ClaimAdminPath}/{Guid.NewGuid()}/approve", new { reviewNote = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("claim_closed", second.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("claim_closed", rejected.Error!.Code);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("claim_not_found", unknown.Error!.Code);
    }

    [Fact]
    public async Task A_listing_that_has_an_owner_no_longer_accepts_claims()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);

        var ownerEmail = TestData.UniqueEmail("owner");
        var ownerClient = factory.CreateSignedInClientAsync(ownerEmail);
        var ownerClaim = await OwnershipApi.ClaimAsync(new AuthApiClient(ownerClient), business.Slug);

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var approved = await new AuthApiClient(staff).PostForSnapshotAsync(
            $"{OwnershipApi.ClaimAdminPath}/{ownerClaim.Id}/approve", new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var strangerClient = factory.CreateSignedInClientAsync(TestData.UniqueEmail("stranger"));
        var stranger = await new AuthApiClient(strangerClient).PostForSnapshotAsync(
            $"/api/businesses/{business.Slug}/claim",
            new { evidence = (string?)null });
        var ownerAgain = await new AuthApiClient(ownerClient).PostForSnapshotAsync(
            $"/api/businesses/{business.Slug}/claim",
            new { evidence = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, stranger.StatusCode);
        Assert.Equal("already_owned", stranger.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, ownerAgain.StatusCode);
        Assert.Equal("already_owner", ownerAgain.Error!.Code);
    }

    [Fact]
    public async Task The_owner_screen_is_scoped_to_the_owner_and_narrow_about_what_it_edits()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var ownerClient = await OwnershipApi.SignInAsync(factory, email);
        var ownerApi = new AuthApiClient(ownerClient);

        var readable = await ownerApi.GetAsync<OwnershipViews.MyBusinessesView>("/api/account/businesses");
        Assert.Contains(readable.Owned, item => item.Slug == slug);

        var description = BusinessApi.LongDescription + " Updated by the owner.";
        var saved = await ownerApi.PutForSnapshotAsync(
            $"/api/account/businesses/{slug}",
            new
            {
                description,
                address = "12 Durbar Marg",
                phone = "9800000021",
                email = "owner@example.com",
                website = "https://example.com",
                openingHours = BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0)),
            });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var readBack = await ownerApi.GetAsync<OwnershipViews.OwnerBusinessView>(
            $"/api/account/businesses/{slug}");
        Assert.Equal(description, readBack.Description);

        // Somebody else's listing answers the same 404 as one that never existed, so this
        // screen cannot be used to discover which addresses are real.
        using var strangerClient = factory.CreateSignedInClientAsync(TestData.UniqueEmail("stranger"));
        var strangerApi = new AuthApiClient(strangerClient);
        using var strangerRead = await strangerApi.GetRawAsync($"/api/account/businesses/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, strangerRead.StatusCode);
        var strangerWrite = await strangerApi.PutForSnapshotAsync(
            $"/api/account/businesses/{slug}",
            new { description = "not mine to edit" });
        Assert.Equal(HttpStatusCode.NotFound, strangerWrite.StatusCode);

        // An address that is not an ordinary web address is refused rather than stored.
        var badUrl = await ownerApi.PutForSnapshotAsync(
            $"/api/account/businesses/{slug}",
            new
            {
                description,
                address = (string?)null,
                phone = (string?)null,
                email = (string?)null,
                website = "javascript:alert(1)",
                openingHours = Array.Empty<object>(),
            });
        Assert.Equal(HttpStatusCode.BadRequest, badUrl.StatusCode);
        Assert.Equal("validation_failed", badUrl.Error!.Code);

        var unchanged = await ownerApi.GetAsync<OwnershipViews.OwnerBusinessView>(
            $"/api/account/businesses/{slug}");
        Assert.Equal(description, unchanged.Description);
    }
}
