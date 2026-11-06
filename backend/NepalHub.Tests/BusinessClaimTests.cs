using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Claims;
using NepalHub.Api.Features.Ownership;

namespace NepalHub.Tests;

/// <summary>
/// Instant claims on published businesses: filing leaves the listing public, the poster
/// sees the claimant on their account page, and only Accept hands over ownership and
/// hides the row. Release after Accept is refused — Accept is terminal.
/// </summary>
public sealed class BusinessClaimTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task Filing_a_claim_requires_an_account()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        using var anonymous = factory.CreateClient();
        var api = new AuthApiClient(anonymous);

        using var response = await api.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Filing_leaves_the_listing_public_and_shows_the_claim_on_both_account_boxes()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);

        using var reader = factory.CreateClient();
        using var before = await reader.GetAsync($"/api/businesses/{business.Slug}");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        var claimantEmail = TestData.UniqueEmail("biz-claimant");
        var claimant = new AuthApiClient(factory.CreateSignedInClientAsync(claimantEmail));
        using var filed = await claimant.PostAsync(
            $"/api/businesses/{business.Slug}/claim",
            new { evidence = "I run this place" });
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);

        var claim = await ContentClaimApi.ReadAsync<ClaimResponse>(filed);
        Assert.Equal(BusinessClaimStatus.Pending, claim.Status);
        Assert.Equal("I run this place", claim.Evidence);
        Assert.Equal(business.Slug, claim.Slug);

        // Unlike jobs, a pending claim does not hide the directory row.
        using var after = await reader.GetAsync($"/api/businesses/{business.Slug}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Businesses.AsNoTracking()
            .SingleAsync(item => item.Id == business.Id);
        Assert.False(stored.IsClaimed);
        Assert.Null(stored.OwnerUserId);

        var overview = await ContentClaimApi.OverviewAsync(claimant);
        Assert.Contains(overview.Mine, item => item.Id == claim.Id && item.Kind == ClaimKind.Business);

        // The staff account that created the listing is the poster.
        var posterEmail = await PosterEmailOfBusinessAsync(business.Slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));
        var posterOverview = await ContentClaimApi.OverviewAsync(poster);
        var posted = Assert.Single(
            posterOverview.OnMyPosts,
            item => item.Id == claim.Id && item.Kind == ClaimKind.Business);
        Assert.Equal(ContentClaimStatus.Pending, posted.Status);
        Assert.Equal(claimantEmail, posted.ClaimantEmail);
        Assert.False(string.IsNullOrWhiteSpace(posted.ClaimantName));
    }

    [Fact]
    public async Task A_second_claimant_is_refused_and_the_same_claimant_cannot_file_twice()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);

        var first = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("biz-first")));
        using var created = await first.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var second = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("biz-second")));
        using var taken = await second.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var takenEnvelope = await taken.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("already_taken", takenEnvelope!.Error!.Code);

        using var again = await first.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var againEnvelope = await again.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("already_claimed", againEnvelope!.Error!.Code);
    }

    [Fact]
    public async Task Accept_hands_over_ownership_hides_the_listing_and_a_later_release_is_refused()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        var claimantEmail = TestData.UniqueEmail("biz-accept");
        var claimant = new AuthApiClient(factory.CreateSignedInClientAsync(claimantEmail));
        using var filed = await claimant.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = "Mine" });
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        var claim = await ContentClaimApi.ReadAsync<ClaimResponse>(filed);

        var posterEmail = await PosterEmailOfBusinessAsync(business.Slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));

        using var stranger = factory.CreateSignedInClientAsync(TestData.UniqueEmail("biz-outside"));
        using var refused = await ContentClaimApi.DecideBusinessAsync(
            new AuthApiClient(stranger), claim.Id, "accept");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        using var accepted = await ContentClaimApi.DecideBusinessAsync(
            poster, claim.Id, "accept", reviewNote: "Welcome aboard");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // Accept hides the row: public detail answers the same 404 as a draft.
        using var reader = factory.CreateClient();
        using var hidden = await reader.GetAsync($"/api/businesses/{business.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Businesses.AsNoTracking()
            .SingleAsync(item => item.Id == business.Id);
        Assert.True(stored.IsClaimed);
        Assert.Equal(await OwnershipApi.UserIdAsync(factory, claimantEmail), stored.OwnerUserId);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = await userManager.FindByEmailAsync(claimantEmail);
        Assert.NotNull(owner);
        Assert.Contains(Roles.Business, await userManager.GetRolesAsync(owner!));

        // Accept is terminal: release answers already_reviewed, not a second decision.
        using var released = await ContentClaimApi.DecideBusinessAsync(poster, claim.Id, "release");
        Assert.Equal(HttpStatusCode.Conflict, released.StatusCode);
        var releaseEnvelope = await released.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("already_reviewed", releaseEnvelope!.Error!.Code);

        // Ownership is read from the account screen while the directory row stays hidden.
        using var reSigned = await OwnershipApi.SignInAsync(factory, claimantEmail);
        var ownerApi = new AuthApiClient(reSigned);
        var mine = await ownerApi.GetAsync<OwnershipViews.MyBusinessesView>("/api/account/businesses");
        Assert.Contains(mine.Owned, item => item.Slug == business.Slug);
    }

    [Fact]
    public async Task Release_of_a_pending_claim_leaves_the_listing_public()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        var claimant = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("biz-release")));
        using var filed = await claimant.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        var claim = await ContentClaimApi.ReadAsync<ClaimResponse>(filed);

        var posterEmail = await PosterEmailOfBusinessAsync(business.Slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));

        using var released = await ContentClaimApi.DecideBusinessAsync(
            poster, claim.Id, "release", reviewNote: "Not the right person");
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);

        using var reader = factory.CreateClient();
        using var stillPublic = await reader.GetAsync($"/api/businesses/{business.Slug}");
        Assert.Equal(HttpStatusCode.OK, stillPublic.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Businesses.AsNoTracking()
            .SingleAsync(item => item.Id == business.Id);
        Assert.False(stored.IsClaimed);
        Assert.Null(stored.OwnerUserId);

        var decided = await dbContext.BusinessClaims.AsNoTracking()
            .SingleAsync(item => item.Id == claim.Id);
        Assert.Equal(BusinessClaimStatus.Released, decided.Status);

        // A released claimant (or anyone else) may try again.
        using var retry = await claimant.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task A_post_with_no_creator_refuses_the_claim()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await dbContext.Businesses.SingleAsync(item => item.Id == business.Id);
        row.CreatedByUserId = null;
        await dbContext.SaveChangesAsync();

        var claimant = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("biz-noposter")));
        using var refused = await claimant.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var envelope = await refused.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("no_poster", envelope!.Error!.Code);

        using var reader = factory.CreateClient();
        using var stillPublic = await reader.GetAsync($"/api/businesses/{business.Slug}");
        Assert.Equal(HttpStatusCode.OK, stillPublic.StatusCode);
    }

    [Fact]
    public async Task Filing_and_deciding_write_audit_lines()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        var claimantEmail = TestData.UniqueEmail("biz-audit");
        var claimant = new AuthApiClient(factory.CreateSignedInClientAsync(claimantEmail));
        using var filed = await claimant.PostAsync(
            $"/api/businesses/{business.Slug}/claim", new { evidence = (string?)null });
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        var claim = await ContentClaimApi.ReadAsync<ClaimResponse>(filed);

        var posterEmail = await PosterEmailOfBusinessAsync(business.Slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));
        using var accepted = await ContentClaimApi.DecideBusinessAsync(
            poster, claim.Id, "accept", reviewNote: "Proof supplied.");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var filedAudit = await Moderations.ReadAuditAsync(
            admin, "?action=business.claimed&pageSize=100");
        Assert.Contains(
            filedAudit.Data!.Items,
            item => item.TargetId == claim.Id.ToString() && item.TargetType == "business_claim");

        var decided = await Moderations.ReadAuditAsync(
            admin, "?action=business.claim_accepted&pageSize=100");
        Assert.Contains(
            decided.Data!.Items,
            item => item.TargetId == claim.Id.ToString() && item.TargetType == "business_claim");
    }

    private async Task<string> PosterEmailOfBusinessAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var business = await dbContext.Businesses.AsNoTracking()
            .Include(item => item.CreatedByUser)
            .SingleAsync(item => item.Slug == slug);

        Assert.NotNull(business.CreatedByUser);

        return business.CreatedByUser!.Email!;
    }
}
