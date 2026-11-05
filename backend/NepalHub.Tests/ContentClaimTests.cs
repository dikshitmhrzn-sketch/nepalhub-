using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

/// <summary>The JSON shapes the content-claim endpoints return.</summary>
internal static class ContentClaimViews
{
    internal sealed record MyClaimView(
        Guid Id,
        SubmissionKind Kind,
        string Slug,
        string Title,
        ContentClaimStatus Status,
        string? Note,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewNote);

    internal sealed record PostedClaimView(
        Guid Id,
        SubmissionKind Kind,
        string Slug,
        string Title,
        string ClaimantName,
        string? ClaimantEmail,
        ContentClaimStatus Status,
        string? Note,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewNote);

    internal sealed record OverviewView(
        IReadOnlyList<MyClaimView> Mine,
        IReadOnlyList<PostedClaimView> OnMyPosts);
}

/// <summary>Shared moves for the content-claim tests.</summary>
internal static class ContentClaimApi
{
    internal const string OverviewPath = "/api/account/content-claims";

    /// <summary>Staff publish a job the claimant can aim at. The staff account is the poster.</summary>
    internal static async Task<string> CreatePublishedJobAsync(NepalHubApiFactory factory)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);
        var slug = JobApi.NewSlug();

        var created = await api.PostForSnapshotAsync(
            "/api/admin/jobs",
            JobApi.Body(slug, $"Claim poster {slug}", published: true));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return slug;
    }

    internal static async Task<string> CreatePublishedDealAsync(NepalHubApiFactory factory)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);
        var slug = $"deal-{Guid.NewGuid():N}";

        var created = await api.PostForSnapshotAsync(
            "/api/admin/deals",
            DealApi.Body(slug, $"A claim offer about {slug}", published: true));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return slug;
    }

    internal static async Task<string> CreatePublishedEventAsync(NepalHubApiFactory factory)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);
        var slug = $"event-{Guid.NewGuid():N}";

        var created = await api.PostForSnapshotAsync(
            "/api/admin/events",
            EventApi.Body(slug, $"A claim event about {slug}", published: true));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return slug;
    }

    internal static async Task<ContentClaimViews.MyClaimView> FileAsync(
        AuthApiClient claimant,
        string kind,
        string slug,
        string? note = null)
    {
        using var response = await claimant.PostAsync(
            $"/api/{kind}/{slug}/claim",
            new { note });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAsync<ContentClaimViews.MyClaimView>(response);
    }

    internal static async Task<HttpResponseMessage> FileRawAsync(
        AuthApiClient claimant,
        string kind,
        string slug,
        string? note = null) =>
        await claimant.PostAsync($"/api/{kind}/{slug}/claim", new { note });

    internal static Task<ContentClaimViews.OverviewView> OverviewAsync(AuthApiClient api) =>
        api.GetAsync<ContentClaimViews.OverviewView>(OverviewPath);

    internal static async Task<HttpResponseMessage> DecideAsync(
        AuthApiClient poster,
        Guid claimId,
        string decision,
        string? reviewNote = null) =>
        await poster.PostAsync(
            $"/api/account/posted-claims/{claimId}/{decision}",
            new { reviewNote });

    internal static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }
}

/// <summary>
/// Instant claims on published jobs, offers and events: filing hides the post, the poster
/// sees the claimant on their account page, and accept or release is the only way it
/// changes again. Everything a stranger could do to somebody else's claim answers the same
/// 404 as an id that never existed.
/// </summary>
public sealed class ContentClaimTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task Filing_a_claim_requires_an_account()
    {
        var slug = await ContentClaimApi.CreatePublishedJobAsync(factory);
        using var anonymous = factory.CreateClient();
        var api = new AuthApiClient(anonymous);

        using var response = await ContentClaimApi.FileRawAsync(api, "jobs", slug);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Filing_hides_the_post_from_readers_and_shows_the_claim_on_both_account_boxes()
    {
        var slug = await ContentClaimApi.CreatePublishedJobAsync(factory);

        using var reader = factory.CreateClient();
        using var before = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        var claimantEmail = TestData.UniqueEmail("content-claimant");
        var claimant = new AuthApiClient(factory.CreateSignedInClientAsync(claimantEmail));
        var claim = await ContentClaimApi.FileAsync(claimant, "jobs", slug, note: "I am the employer");
        Assert.Equal(SubmissionKind.Job, claim.Kind);
        Assert.Equal(ContentClaimStatus.Pending, claim.Status);
        Assert.Equal("I am the employer", claim.Note);
        Assert.Equal(slug, claim.Slug);

        // The hide is immediate: list and detail both drop the row through PublicContentScope.
        using var after = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);

        using var list = await reader.GetAsync("/api/jobs?pageSize=5");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listJson = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(slug, listJson);

        // The claimant's box holds their claim.
        var claimantOverview = await ContentClaimApi.OverviewAsync(claimant);
        Assert.Contains(claimantOverview.Mine, item => item.Id == claim.Id);
        Assert.DoesNotContain(claimantOverview.OnMyPosts, item => item.Id == claim.Id);

        // The poster — the staff account that created the row — sees the claimant's details.
        var posterEmail = await PosterEmailOfJobAsync(slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));
        var posterOverview = await ContentClaimApi.OverviewAsync(poster);
        var posted = Assert.Single(posterOverview.OnMyPosts, item => item.Id == claim.Id);
        Assert.Equal(ContentClaimStatus.Pending, posted.Status);
        Assert.Equal(claimantEmail, posted.ClaimantEmail);
        Assert.False(string.IsNullOrWhiteSpace(posted.ClaimantName));
        Assert.DoesNotContain(posterOverview.Mine, item => item.Id == claim.Id);

        // A stranger's boxes never see it.
        var stranger = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-stranger")));
        var strangerOverview = await ContentClaimApi.OverviewAsync(stranger);
        Assert.DoesNotContain(strangerOverview.Mine, item => item.Id == claim.Id);
        Assert.DoesNotContain(strangerOverview.OnMyPosts, item => item.Id == claim.Id);
    }

    [Fact]
    public async Task A_second_claimant_is_refused_and_the_same_claimant_cannot_file_twice()
    {
        var slug = await ContentClaimApi.CreatePublishedJobAsync(factory);

        var first = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-first")));
        var claim = await ContentClaimApi.FileAsync(first, "jobs", slug);

        var second = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-second")));
        using var taken = await ContentClaimApi.FileRawAsync(second, "jobs", slug);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var takenEnvelope = await taken.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("already_taken", takenEnvelope!.Error!.Code);

        using var again = await ContentClaimApi.FileRawAsync(first, "jobs", slug);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var againEnvelope = await again.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("already_claimed", againEnvelope!.Error!.Code);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await dbContext.Jobs.AsNoTracking().SingleAsync(item => item.Slug == slug);
        var active = await dbContext.ContentClaims.AsNoTracking()
            .CountAsync(item => item.JobId == job.Id && item.Status != ContentClaimStatus.Released);
        Assert.Equal(1, active);

        var stored = await dbContext.Jobs.AsNoTracking().SingleAsync(item => item.Slug == slug);
        Assert.True(stored.IsClaimed);
        _ = claim;
    }

    [Fact]
    public async Task Accept_keeps_the_post_hidden_and_a_later_release_is_refused_once_settled()
    {
        var slug = await ContentClaimApi.CreatePublishedJobAsync(factory);
        var claimant = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-decide")));
        var claim = await ContentClaimApi.FileAsync(claimant, "jobs", slug);

        var posterEmail = await PosterEmailOfJobAsync(slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));

        using var stranger = factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-outside"));
        using var refused = await ContentClaimApi.DecideAsync(
            new AuthApiClient(stranger), claim.Id, "accept");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        using var accepted = await ContentClaimApi.DecideAsync(
            poster, claim.Id, "accept", reviewNote: "This is ours");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var acceptedClaim = await ContentClaimApi.ReadAsync<ContentClaimViews.PostedClaimView>(accepted);
        Assert.Equal(ContentClaimStatus.Accepted, acceptedClaim.Status);
        Assert.Equal("This is ours", acceptedClaim.ReviewNote);

        using var reader = factory.CreateClient();
        using var stillHidden = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, stillHidden.StatusCode);

        // The decision does not re-run.
        using var again = await ContentClaimApi.DecideAsync(poster, claim.Id, "release");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        using var stillGone = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, stillGone.StatusCode);
    }

    [Fact]
    public async Task Release_restores_the_post_and_a_new_claim_can_file_again()
    {
        var slug = await ContentClaimApi.CreatePublishedJobAsync(factory);
        var claimant = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-release")));
        var claim = await ContentClaimApi.FileAsync(claimant, "jobs", slug);

        var posterEmail = await PosterEmailOfJobAsync(slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));

        using var reader = factory.CreateClient();
        using var hidden = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var released = await ContentClaimApi.DecideAsync(
            poster, claim.Id, "release", reviewNote: "Wrong person");
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        var releasedClaim = await ContentClaimApi.ReadAsync<ContentClaimViews.PostedClaimView>(released);
        Assert.Equal(ContentClaimStatus.Released, releasedClaim.Status);

        using var back = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.OK, back.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Jobs.AsNoTracking().SingleAsync(item => item.Slug == slug);
        Assert.False(stored.IsClaimed);

        // A released claimant (or anyone else) may try again.
        var retry = await ContentClaimApi.FileAsync(claimant, "jobs", slug);
        Assert.Equal(ContentClaimStatus.Pending, retry.Status);
    }

    [Fact]
    public async Task Offers_and_events_are_claimable_and_leave_the_public_pages()
    {
        var dealSlug = await ContentClaimApi.CreatePublishedDealAsync(factory);
        var eventSlug = await ContentClaimApi.CreatePublishedEventAsync(factory);

        var claimant = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-kinds")));

        var dealClaim = await ContentClaimApi.FileAsync(claimant, "deals", dealSlug);
        Assert.Equal(SubmissionKind.Deal, dealClaim.Kind);

        var eventClaim = await ContentClaimApi.FileAsync(claimant, "events", eventSlug);
        Assert.Equal(SubmissionKind.Event, eventClaim.Kind);

        using var reader = factory.CreateClient();
        using var dealGone = await reader.GetAsync($"/api/deals/{dealSlug}");
        Assert.Equal(HttpStatusCode.NotFound, dealGone.StatusCode);

        using var eventGone = await reader.GetAsync($"/api/events/{eventSlug}");
        Assert.Equal(HttpStatusCode.NotFound, eventGone.StatusCode);

        var overview = await ContentClaimApi.OverviewAsync(claimant);
        Assert.Equal(2, overview.Mine.Count);
    }

    [Fact]
    public async Task An_unknown_or_closed_slug_answers_the_same_404()
    {
        var claimant = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-missing")));

        using var unknown = await ContentClaimApi.FileRawAsync(claimant, "jobs", "no-such-job");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // A draft is not public, so it answers the same 404 rather than confirming it exists.
        var draftSlug = JobApi.NewSlug();
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var created = await new AuthApiClient(staff).PostForSnapshotAsync(
            "/api/admin/jobs",
            JobApi.Body(draftSlug, $"A draft {draftSlug}", published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var draft = await ContentClaimApi.FileRawAsync(claimant, "jobs", draftSlug);
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
    }

    [Fact]
    public async Task A_post_with_no_creator_refuses_the_claim()
    {
        var slug = await ContentClaimApi.CreatePublishedJobAsync(factory);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await dbContext.Jobs.SingleAsync(item => item.Slug == slug);
        job.CreatedByUserId = null;
        await dbContext.SaveChangesAsync();

        var claimant = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("content-noposter")));

        using var refused = await ContentClaimApi.FileRawAsync(claimant, "jobs", slug);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var envelope = await refused.Content
            .ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("no_poster", envelope!.Error!.Code);

        using var reader = factory.CreateClient();
        using var stillPublic = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.OK, stillPublic.StatusCode);
    }

    [Fact]
    public async Task Filing_and_deciding_write_audit_lines()
    {
        var slug = await ContentClaimApi.CreatePublishedJobAsync(factory);
        var claimantEmail = TestData.UniqueEmail("content-audit");
        var claimant = new AuthApiClient(factory.CreateSignedInClientAsync(claimantEmail));
        var claim = await ContentClaimApi.FileAsync(claimant, "jobs", slug);

        var posterEmail = await PosterEmailOfJobAsync(slug);
        var poster = new AuthApiClient(await OwnershipApi.SignInAsync(factory, posterEmail));
        using var released = await ContentClaimApi.DecideAsync(poster, claim.Id, "release");
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var filed = await Moderations.ReadAuditAsync(
            admin, "?action=content.claimed&pageSize=100");
        Assert.Contains(
            filed.Data!.Items,
            item => item.TargetId == claim.Id.ToString() && item.TargetType == "ContentClaim");

        var decided = await Moderations.ReadAuditAsync(
            admin, "?action=content.claim_released&pageSize=100");
        Assert.Contains(
            decided.Data!.Items,
            item => item.TargetId == claim.Id.ToString() && item.TargetType == "ContentClaim");
    }

    private async Task<string> PosterEmailOfJobAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await dbContext.Jobs.AsNoTracking()
            .Include(item => item.CreatedByUser)
            .SingleAsync(item => item.Slug == slug);

        Assert.NotNull(job.CreatedByUser);

        return job.CreatedByUser!.Email!;
    }
}
