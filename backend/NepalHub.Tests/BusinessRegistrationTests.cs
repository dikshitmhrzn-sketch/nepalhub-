using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

/// <summary>The JSON shapes this file reads back, kept as small as each assertion needs.</summary>
internal static class RegistrationViews
{
    internal sealed record VerificationView(
        Guid Id,
        Guid BusinessId,
        string Slug,
        string BusinessName,
        BusinessVerificationStatus Status,
        string? Note,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewNote);

    internal sealed record OwnedView(
        Guid Id,
        string Slug,
        string Name,
        bool IsPublished,
        bool IsVerified);

    internal sealed record ClaimView(Guid Id, string Slug, BusinessClaimStatus Status);

    internal sealed record MyBusinessesView(
        IReadOnlyList<OwnedView> Owned,
        IReadOnlyList<ClaimView> Claims,
        IReadOnlyList<VerificationView> Verifications);

    internal sealed record OwnerView(
        Guid Id,
        string Slug,
        string Name,
        string Description,
        bool IsPublished,
        bool IsVerified,
        VerificationView? Verification);

    internal sealed record QueueView(
        IReadOnlyList<QueueRow> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasPrevious,
        bool HasNext);

    internal sealed record QueueRow(
        Guid Id,
        Guid BusinessId,
        string Slug,
        string BusinessName,
        string OwnerName,
        string? OwnerEmail,
        BusinessVerificationStatus Status,
        string? Note,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewNote);

    internal sealed record DashboardView(DashboardAttentionView NeedsAttention);

    internal sealed record DashboardAttentionView(int PendingVerifications);
}

/// <summary>Shared moves for the registration and verification tests.</summary>
internal static class RegistrationApi
{
    internal const string AccountPath = "/api/account/businesses";
    internal const string QueuePath = "/api/admin/business-verifications";

    /// <summary>
    /// A registration body. The default description is the publishable one, so a fixture that
    /// does not say otherwise is a listing staff can approve rather than one they must refuse,
    /// and a test that wants the opposite says so.
    /// </summary>
    internal static object RegisterBody(
        string slug,
        string? description = null,
        string districtCode = "bagmati-lalitpur",
        string? phone = "9800000401") => new
    {
        slug,
        name = $"Registered {slug}",
        description = description ?? BusinessApi.LongDescription,
        category = "Restaurants",
        districtCode,
        address = (string?)null,
        phone,
        email = (string?)null,
        website = (string?)null,
        openingHours = Array.Empty<object>(),
    };

    internal static async Task<RegistrationViews.OwnerView> RegisterAsync(
        AuthApiClient api,
        string slug,
        string? description = null,
        string districtCode = "bagmati-lalitpur",
        string? phone = "9800000401")
    {
        var snapshot = await api.PostForSnapshotAsync(
            AccountPath,
            RegisterBody(slug, description, districtCode, phone));

        Assert.Equal(HttpStatusCode.Created, snapshot.StatusCode);

        return Read<RegistrationViews.OwnerView>(snapshot);
    }

    internal static async Task<RegistrationViews.VerificationView> SubmitAsync(
        AuthApiClient api,
        string slug,
        string? note = null)
    {
        var snapshot = await api.PostForSnapshotAsync(
            $"{AccountPath}/{slug}/verification",
            new { note });

        Assert.Equal(HttpStatusCode.Created, snapshot.StatusCode);

        return Read<RegistrationViews.VerificationView>(snapshot);
    }

    internal static async Task<RegistrationViews.QueueView> ReadQueueAsync(HttpClient client, string query = "")
    {
        using var response = await client.GetAsync($"{QueuePath}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<RegistrationViews.QueueView>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    internal static T Read<T>(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<T>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }
}

/// <summary>
/// The host this file registers against. The business quota — registration and the
/// verification request both — is read from the environment while the host starts, and
/// every fixture overwrites that variable with its own values, so this host starts during
/// its own construction: while the variable still holds the generous default, before the
/// limited fixture below can lower it for the whole process.
/// </summary>
public sealed class RegistrationApiFactory : NepalHubApiFactory
{
    public RegistrationApiFactory()
    {
        _ = Services;
    }
}

/// <summary>
/// Registering a listing. The three endpoints are the account's own: a member registers a
/// draft of their own, asks staff to check it, and edits it while it is still theirs to edit.
/// Everything a stranger could do to somebody else's address answers the same 404 as an
/// address that never existed.
/// </summary>
public sealed class BusinessRegistrationTests(RegistrationApiFactory factory)
    : IClassFixture<RegistrationApiFactory>
{
    [Fact]
    public async Task Registering_requires_an_account_and_creates_a_draft_the_directory_does_not_serve()
    {
        var slug = BusinessApi.NewSlug();

        using var anonymous = factory.CreateClient();
        var refused = await new AuthApiClient(anonymous).PostForSnapshotAsync(
            RegistrationApi.AccountPath,
            RegistrationApi.RegisterBody(slug));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        var email = TestData.UniqueEmail("reg-owner");
        var client = factory.CreateSignedInClientAsync(email);
        var api = new AuthApiClient(client);

        var created = await RegistrationApi.RegisterAsync(api, slug);
        Assert.Equal(slug, created.Slug);
        Assert.False(created.IsPublished);
        Assert.False(created.IsVerified);
        Assert.Null(created.Verification);

        // The row is born owned rather than merely created by: the owner screen and nobody
        // else's is where it appears.
        var mine = await api.GetAsync<RegistrationViews.MyBusinessesView>(RegistrationApi.AccountPath);
        var row = Assert.Single(mine.Owned, item => item.Slug == slug);
        Assert.False(row.IsPublished);
        Assert.False(row.IsVerified);
        Assert.Empty(mine.Verifications);

        // Publication is what approval grants, so a draft has no public address yet, and it
        // answers the same 404 as one that never existed.
        using var reader = factory.CreateClient();
        using var hidden = await reader.GetAsync($"/api/businesses/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Registration_refuses_a_slug_that_is_taken_or_reserved_and_a_district_it_does_not_know()
    {
        var existing = await OwnershipApi.CreateBusinessAsync(factory);
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-refused")));

        var taken = await api.PostForSnapshotAsync(
            RegistrationApi.AccountPath,
            RegistrationApi.RegisterBody(existing.Slug));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("slug_taken", taken.Error!.Code);

        var reserved = await api.PostForSnapshotAsync(
            RegistrationApi.AccountPath,
            RegistrationApi.RegisterBody("featured"));
        Assert.Equal(HttpStatusCode.BadRequest, reserved.StatusCode);
        Assert.Equal("validation_failed", reserved.Error!.Code);
        Assert.Contains(BusinessApi.Details(reserved), error => error.Field == "slug");

        var unknownDistrict = await api.PostForSnapshotAsync(
            RegistrationApi.AccountPath,
            RegistrationApi.RegisterBody(BusinessApi.NewSlug(), districtCode: "not-a-district"));
        Assert.Equal(HttpStatusCode.BadRequest, unknownDistrict.StatusCode);
        Assert.Contains(
            BusinessApi.Details(unknownDistrict), error => error.Field == "districtCode");
    }

    [Fact]
    public async Task Submitting_opens_one_request_and_the_queue_holds_it_pending()
    {
        var email = TestData.UniqueEmail("reg-submitter");
        var slug = BusinessApi.NewSlug();
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(email));
        await RegistrationApi.RegisterAsync(api, slug);

        var request = await RegistrationApi.SubmitAsync(
            api, slug, "Hand-made momo, same kitchen since 2010.");
        Assert.Equal(BusinessVerificationStatus.Pending, request.Status);
        Assert.Equal(slug, request.Slug);
        Assert.Equal("Hand-made momo, same kitchen since 2010.", request.Note);
        Assert.Null(request.ReviewedAt);

        var again = await api.PostForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{slug}/verification",
            new { note = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("verification_pending", again.Error!.Code);

        // Somebody else's listing answers the same 404 as one that never existed.
        var strangerApi = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-stranger")));
        var stranger = await strangerApi.PostForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{slug}/verification",
            new { note = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal("business_not_found", stranger.Error!.Code);

        // A member of the site cannot read the queue this entered.
        using var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-bystander"));
        using var forbidden = await member.GetAsync(RegistrationApi.QueuePath);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var queue = await RegistrationApi.ReadQueueAsync(moderator, "?pageSize=100");
        var row = Assert.Single(queue.Items, item => item.Id == request.Id);
        Assert.Equal(slug, row.Slug);
        Assert.Equal(email, row.OwnerEmail);
        Assert.Equal(BusinessVerificationStatus.Pending, row.Status);

        // A typo in the filter is refused rather than quietly returning the whole table.
        using var nonsense = await moderator.GetAsync($"{RegistrationApi.QueuePath}?status=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
        Assert.Contains("status_invalid", await nonsense.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Approving_publishes_verifies_grants_the_role_and_leaves_one_audit_line()
    {
        var email = TestData.UniqueEmail("reg-approve");
        var slug = BusinessApi.NewSlug();
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(email));
        await RegistrationApi.RegisterAsync(api, slug, phone: "9800000411");
        var request = await RegistrationApi.SubmitAsync(api, slug);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var reviewed = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/approve",
            new { reviewNote = "Called the number; someone answered." });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);

        var decided = RegistrationApi.Read<RegistrationViews.QueueRow>(reviewed);
        Assert.Equal(BusinessVerificationStatus.Approved, decided.Status);
        Assert.Equal("Called the number; someone answered.", decided.ReviewNote);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbContext.Businesses.AsNoTracking()
            .SingleAsync(item => item.Slug == slug);
        Assert.True(stored.IsPublished);
        Assert.True(stored.IsVerified);
        Assert.NotNull(stored.VerifiedAt);

        var settled = await dbContext.BusinessVerificationRequests.AsNoTracking()
            .SingleAsync(item => item.Id == request.Id);
        Assert.Equal(BusinessVerificationStatus.Approved, settled.Status);
        Assert.NotNull(settled.ReviewedAt);

        // The role claim is baked into the cookie, so the owner signs in again to see it.
        using var reSigned = await OwnershipApi.SignInAsync(factory, email);
        var session = await new AuthApiClient(reSigned).GetAsync<SessionResponse>("/api/auth/session");
        Assert.Contains(Roles.Business, session.User!.Roles);

        using var reader = factory.CreateClient();
        using var live = await reader.GetAsync($"/api/businesses/{slug}");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        // A moderator performed the decision, but only an admin may read the log.
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var audit = await Moderations.ReadAuditAsync(
            admin, "?action=business.verification_approved&pageSize=100");
        var entry = Assert.Single(
            audit.Data!.Items,
            item => item.TargetId == request.Id.ToString());
        Assert.Equal("business_verification", entry.TargetType);
        Assert.Contains("Approved", entry.After);
    }

    [Fact]
    public async Task Approving_a_listing_that_fails_the_publish_rules_is_refused_and_leaves_the_request_open()
    {
        var slug = BusinessApi.NewSlug();
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-blocked")));

        // A draft may be as thin as its owner likes: the publish rules are what staff hold
        // the line at, and a phone number and a real description are two of them.
        await RegistrationApi.RegisterAsync(
            api, slug, description: "Too short.", phone: null);
        var request = await RegistrationApi.SubmitAsync(api, slug);

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var refused = await new AuthApiClient(staff).PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("not_publishable", refused.Error!.Code);

        var blockers = BusinessApi.Details(refused);
        Assert.Contains(blockers, error => error.Field == "phone");
        Assert.Contains(blockers, error => error.Field == "description");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbContext.Businesses.AsNoTracking()
            .SingleAsync(item => item.Slug == slug);
        Assert.False(stored.IsPublished);
        Assert.False(stored.IsVerified);

        var stillOpen = await dbContext.BusinessVerificationRequests.AsNoTracking()
            .SingleAsync(item => item.Id == request.Id);
        Assert.Equal(BusinessVerificationStatus.Pending, stillOpen.Status);
        Assert.Null(stillOpen.ReviewedAt);
    }

    [Fact]
    public async Task A_settled_request_cannot_be_decided_a_second_time_and_a_live_listing_cannot_ask_again()
    {
        var slug = BusinessApi.NewSlug();
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-settled")));
        await RegistrationApi.RegisterAsync(api, slug, phone: "9800000412");
        var request = await RegistrationApi.SubmitAsync(api, slug);

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(staff);

        var first = await adminApi.PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await adminApi.PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/approve",
            new { reviewNote = (string?)null });
        var rejectedAfterwards = await adminApi.PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/reject",
            new { reviewNote = (string?)null });
        var unknown = await adminApi.PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{Guid.NewGuid()}/approve",
            new { reviewNote = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("verification_closed", second.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, rejectedAfterwards.StatusCode);
        Assert.Equal("verification_closed", rejectedAfterwards.Error!.Code);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("verification_not_found", unknown.Error!.Code);

        // It is live now, so there is nothing left to ask staff to approve.
        var askAgain = await api.PostForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{slug}/verification",
            new { note = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, askAgain.StatusCode);
        Assert.Equal("already_published", askAgain.Error!.Code);
    }

    [Fact]
    public async Task Rejecting_leaves_the_draft_unpublished_grants_nothing_and_lets_the_owner_ask_again()
    {
        var email = TestData.UniqueEmail("reg-rejected");
        var slug = BusinessApi.NewSlug();
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(email));
        await RegistrationApi.RegisterAsync(api, slug, phone: "9800000413");
        var request = await RegistrationApi.SubmitAsync(api, slug);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var rejected = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/reject",
            new { reviewNote = "The description reads like an advertisement, not a listing." });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        var decided = RegistrationApi.Read<RegistrationViews.QueueRow>(rejected);
        Assert.Equal(BusinessVerificationStatus.Rejected, decided.Status);
        Assert.Equal(
            "The description reads like an advertisement, not a listing.", decided.ReviewNote);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbContext.Businesses.AsNoTracking()
            .SingleAsync(item => item.Slug == slug);
        Assert.False(stored.IsPublished);
        Assert.False(stored.IsVerified);
        Assert.Null(stored.VerifiedAt);

        // Nothing was granted: the owner still holds no business role.
        using var reSigned = await OwnershipApi.SignInAsync(factory, email);
        var session = await new AuthApiClient(reSigned).GetAsync<SessionResponse>("/api/auth/session");
        Assert.DoesNotContain(Roles.Business, session.User!.Roles);

        using var reader = factory.CreateClient();
        using var hidden = await reader.GetAsync($"/api/businesses/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        // A rejection is an answer to one question, not a ban: the owner may correct the
        // listing and ask again.
        var secondAsk = await api.PostForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{slug}/verification",
            new { note = "Rewritten; the kitchen's own words now." });
        Assert.Equal(HttpStatusCode.Created, secondAsk.StatusCode);
        Assert.Equal(
            BusinessVerificationStatus.Pending,
            RegistrationApi.Read<RegistrationViews.VerificationView>(secondAsk).Status);
    }

    [Fact]
    public async Task The_owner_may_correct_a_drafts_identity_but_not_a_published_ones()
    {
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-editor")));

        var draftSlug = BusinessApi.NewSlug();
        await RegistrationApi.RegisterAsync(api, draftSlug, phone: "9800000421");

        var renamedSlug = BusinessApi.NewSlug();
        var renamed = await api.PutForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{draftSlug}",
            new
            {
                slug = renamedSlug,
                name = "Momo House Renamed",
                category = "Shops",
                districtCode = "bagmati-kathmandu",
                description = BusinessApi.LongDescription,
                phone = "9800000421",
                address = (string?)null,
                email = (string?)null,
                website = (string?)null,
                openingHours = Array.Empty<object>(),
            });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var updated = RegistrationApi.Read<RegistrationViews.OwnerView>(renamed);
        Assert.Equal(renamedSlug, updated.Slug);
        Assert.Equal("Momo House Renamed", updated.Name);

        // The draft moved, and its old address now answers as any address without a listing.
        using var moved = await api.GetRawAsync($"{RegistrationApi.AccountPath}/{draftSlug}");
        Assert.Equal(HttpStatusCode.NotFound, moved.StatusCode);

        // A slug another listing already uses is a field error naming the slug, rather than
        // a silent keep of the old one.
        var existing = await OwnershipApi.CreateBusinessAsync(factory);
        var taken = await api.PutForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{renamedSlug}",
            new
            {
                slug = existing.Slug,
                description = BusinessApi.LongDescription,
                phone = "9800000421",
            });
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Equal("validation_failed", taken.Error!.Code);
        Assert.Contains(BusinessApi.Details(taken), error => error.Field == "slug");

        // Published: the address and the facts belong to staff now.
        var liveSlug = BusinessApi.NewSlug();
        await RegistrationApi.RegisterAsync(api, liveSlug, phone: "9800000422");
        var request = await RegistrationApi.SubmitAsync(api, liveSlug);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var approved = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var nameChange = await api.PutForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{liveSlug}",
            new
            {
                name = "Renamed After Publication",
                description = BusinessApi.LongDescription,
                phone = "9800000422",
            });
        Assert.Equal(HttpStatusCode.BadRequest, nameChange.StatusCode);
        Assert.Contains(
            BusinessApi.Details(nameChange),
            error => error is { Field: "name", Code: "immutable" });

        var slugChange = await api.PutForSnapshotAsync(
            $"{RegistrationApi.AccountPath}/{liveSlug}",
            new
            {
                slug = BusinessApi.NewSlug(),
                description = BusinessApi.LongDescription,
                phone = "9800000422",
            });
        Assert.Equal(HttpStatusCode.BadRequest, slugChange.StatusCode);
        Assert.Contains(
            BusinessApi.Details(slugChange),
            error => error is { Field: "slug", Code: "immutable" });
    }

    [Fact]
    public async Task The_dashboard_counts_this_queue_as_work_to_do()
    {
        var slug = BusinessApi.NewSlug();
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("reg-dashboard")));
        await RegistrationApi.RegisterAsync(api, slug, phone: "9800000431");
        var request = await RegistrationApi.SubmitAsync(api, slug);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(admin);

        var before = await adminApi.GetAsync<RegistrationViews.DashboardView>(
            "/api/admin/dashboard");
        Assert.True(before.NeedsAttention.PendingVerifications >= 1);

        var approved = await adminApi.PostForSnapshotAsync(
            $"{RegistrationApi.QueuePath}/{request.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var after = await adminApi.GetAsync<RegistrationViews.DashboardView>(
            "/api/admin/dashboard");
        Assert.Equal(
            before.NeedsAttention.PendingVerifications - 1,
            after.NeedsAttention.PendingVerifications);
    }
}
