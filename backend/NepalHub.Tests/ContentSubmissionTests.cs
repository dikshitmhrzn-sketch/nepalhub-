using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Tests;

/// <summary>The JSON shapes this file reads back, kept as small as each assertion needs.</summary>
internal static class SubmissionViews
{
    internal sealed record SubmissionView(
        Guid Id,
        SubmissionKind Kind,
        Guid ItemId,
        string Slug,
        string Title,
        bool IsPublished,
        SubmissionStatus Status,
        string? Note,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewNote);

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
        SubmissionKind Kind,
        Guid ItemId,
        string Slug,
        string Title,
        bool IsPublished,
        string SubmitterName,
        string? SubmitterEmail,
        SubmissionStatus Status,
        string? Note,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewNote);

    internal sealed record DashboardView(DashboardAttentionView NeedsAttention);

    internal sealed record DashboardAttentionView(int PendingSubmissions);

    internal sealed record DetailView(
        SubmissionView Submission,
        DetailContent? Job,
        DetailContent? Event,
        DetailContent? Deal);

    /// <summary>The draft body as the author's editor reads it back, trimmed to what these tests assert.</summary>
    internal sealed record DetailContent(
        string Slug,
        string Title,
        string Description,
        bool IsPublished,
        string? DistrictCode);
}

/// <summary>Shared moves for the content-submission tests.</summary>
internal static class SubmissionApi
{
    internal const string AccountPath = "/api/account/submissions";
    internal const string QueuePath = "/api/admin/content-submissions";

    /// <summary>
    /// A job submission body. The fixture's own description is the publishable one, so a
    /// test that does not say otherwise sends a draft staff can approve rather than one
    /// they must refuse, and a test that wants the opposite says so.
    /// </summary>
    internal static object JobSubmission(
        string slug,
        string? note = null,
        bool published = false,
        string? description = null) => new
    {
        content = JobApi.Body(
            slug,
            organization: $"An employer about {slug}",
            published,
            description: description),
        note,
    };

    internal static object EventSubmission(string slug, string title, string? note = null) => new
    {
        content = EventApi.Body(slug, title, published: false),
        note,
    };

    internal static object DealSubmission(string slug, string title, string? note = null) => new
    {
        content = DealApi.Body(slug, title, published: false),
        note,
    };

    internal static async Task<SubmissionViews.SubmissionView> SubmitJobAsync(
        AuthApiClient api,
        string slug,
        string? note = null,
        bool published = false,
        string? description = null)
    {
        var snapshot = await api.PostForSnapshotAsync(
            $"{AccountPath}/jobs",
            JobSubmission(slug, note, published, description));

        Assert.Equal(HttpStatusCode.Created, snapshot.StatusCode);

        return Read<SubmissionViews.SubmissionView>(snapshot);
    }

    internal static async Task<SubmissionViews.QueueView> ReadQueueAsync(HttpClient client, string query = "")
    {
        using var response = await client.GetAsync($"{QueuePath}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<SubmissionViews.QueueView>>(TestJson.Options);
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
/// The host this file registers against. The submission quota is read from the environment
/// while the host starts, and every fixture overwrites that variable with its own values,
/// so this host starts during its own construction: while the variable still holds the
/// generous default, before a limited fixture can lower it for the whole process.
/// </summary>
public sealed class SubmissionApiFactory : NepalHubApiFactory
{
    public SubmissionApiFactory()
    {
        _ = Services;
    }
}

/// <summary>
/// Sending content in. A member writes a draft, the queue holds it, staff approve or turn
/// it down, and only the approval flips it live — the business-verification workflow
/// applied to jobs, events and offers. Everything a stranger could do to somebody else's
/// submission answers the same 404 as an id that never existed.
/// </summary>
public sealed class ContentSubmissionTests(SubmissionApiFactory factory)
    : IClassFixture<SubmissionApiFactory>
{
    [Fact]
    public async Task Submitting_creates_an_unpublished_draft_hides_it_from_readers_and_holds_it_in_the_queue()
    {
        var email = TestData.UniqueEmail("sub-author");
        var slug = JobApi.NewSlug();
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(email));

        // Anonymous: the fallback policy is the entire test, as on every account screen.
        using var anonymous = factory.CreateClient();
        var refused = await new AuthApiClient(anonymous).PostForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/jobs",
            SubmissionApi.JobSubmission(slug));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // The body may claim a publish; the service forces the flag off, because going
        // live is the one thing a submission exists to ask staff for.
        var created = await SubmissionApi.SubmitJobAsync(
            api,
            slug,
            note: "Posted by the employer's own HR desk.",
            published: true);
        Assert.Equal(SubmissionKind.Job, created.Kind);
        Assert.Equal(SubmissionStatus.Pending, created.Status);
        Assert.False(created.IsPublished);
        Assert.Equal("Posted by the employer's own HR desk.", created.Note);
        Assert.Null(created.ReviewedAt);

        // The draft has no public address yet and answers as any address without a listing.
        using var reader = factory.CreateClient();
        using var hidden = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        // The author's list holds their own work, and only theirs.
        var mine = await api.GetAsync<IReadOnlyList<SubmissionViews.SubmissionView>>(
            SubmissionApi.AccountPath);
        Assert.Contains(mine, item => item.Id == created.Id && item.Slug == slug);

        var strangerApi = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("sub-stranger")));
        var strangerList = await strangerApi.GetAsync<IReadOnlyList<SubmissionViews.SubmissionView>>(
            SubmissionApi.AccountPath);
        Assert.DoesNotContain(strangerList, item => item.Id == created.Id);

        // Somebody else's submission answers the same 404 as one that never existed.
        var strangerEdit = await strangerApi.PutForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}",
            new { kind = "Job", job = JobApi.Body(slug, "Another employer", published: false) });
        Assert.Equal(HttpStatusCode.NotFound, strangerEdit.StatusCode);
        Assert.Equal("submission_not_found", strangerEdit.Error!.Code);

        var strangerResend = await strangerApi.PostForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}/resubmit",
            body: null);
        Assert.Equal(HttpStatusCode.NotFound, strangerResend.StatusCode);

        // A member of the site cannot read the queue this entered.
        using var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("sub-bystander"));
        using var forbidden = await member.GetAsync(SubmissionApi.QueuePath);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var queue = await SubmissionApi.ReadQueueAsync(moderator, "?pageSize=100");
        var row = Assert.Single(queue.Items, item => item.Id == created.Id);
        Assert.Equal(slug, row.Slug);
        Assert.Equal(email, row.SubmitterEmail);
        Assert.Equal(SubmissionKind.Job, row.Kind);
        Assert.Equal(SubmissionStatus.Pending, row.Status);

        // A typo in a filter is refused rather than quietly returning the whole table.
        using var nonsense = await moderator.GetAsync($"{SubmissionApi.QueuePath}?kind=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
        Assert.Contains("kind_invalid", await nonsense.Content.ReadAsStringAsync());

        using var statusNonsense = await moderator.GetAsync($"{SubmissionApi.QueuePath}?status=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, statusNonsense.StatusCode);
        Assert.Contains("status_invalid", await statusNonsense.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Approving_publishes_the_draft_writes_the_audit_line_and_closes_the_queue_row()
    {
        var slug = JobApi.NewSlug();
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("sub-approve")));
        var created = await SubmissionApi.SubmitJobAsync(api, slug);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var reviewed = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{created.Id}/approve",
            new { reviewNote = "Employer confirmed the posting by phone." });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);

        var decided = SubmissionApi.Read<SubmissionViews.QueueRow>(reviewed);
        Assert.Equal(SubmissionStatus.Approved, decided.Status);
        Assert.Equal("Employer confirmed the posting by phone.", decided.ReviewNote);
        Assert.True(decided.IsPublished);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbContext.Jobs.AsNoTracking()
            .SingleAsync(item => item.Slug == slug);
        Assert.True(stored.IsPublished);

        var settled = await dbContext.ContentSubmissions.AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);
        Assert.Equal(SubmissionStatus.Approved, settled.Status);
        Assert.NotNull(settled.ReviewedAt);

        // The draft is a real listing now, served to any reader.
        using var reader = factory.CreateClient();
        using var live = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        // A moderator performed the decision, but only an admin may read the log.
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var audit = await Moderations.ReadAuditAsync(
            admin, "?action=submission.approved&pageSize=100");
        var entry = Assert.Single(
            audit.Data!.Items,
            item => item.TargetId == created.Id.ToString());
        Assert.Equal("content_submission", entry.TargetType);
        Assert.Contains("Approved", entry.After);

        // The queue does not re-decide, whichever way it is asked again.
        var adminApi = new AuthApiClient(admin);
        var second = await adminApi.PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{created.Id}/approve",
            new { reviewNote = (string?)null });
        var rejectedAfterwards = await adminApi.PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{created.Id}/reject",
            new { reviewNote = (string?)null });
        var unknown = await adminApi.PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{Guid.NewGuid()}/approve",
            new { reviewNote = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("submission_closed", second.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, rejectedAfterwards.StatusCode);
        Assert.Equal("submission_closed", rejectedAfterwards.Error!.Code);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("submission_not_found", unknown.Error!.Code);

        // Live: the author's own editor no longer takes corrections either.
        var editAfterApproval = await api.PutForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}",
            new { kind = "Job", job = JobApi.Body(slug, "An employer after approval", published: false) });
        Assert.Equal(HttpStatusCode.Conflict, editAfterApproval.StatusCode);
        Assert.Equal("already_published", editAfterApproval.Error!.Code);
    }

    [Fact]
    public async Task Approving_a_draft_that_fails_the_publish_rules_is_refused_and_leaves_it_open()
    {
        var slug = JobApi.NewSlug();
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("sub-blocked")));

        // A draft may be as thin as its author likes: the publish rules are what staff
        // hold the line at, and ten characters of description is one of them.
        var created = await SubmissionApi.SubmitJobAsync(
            api, slug, description: "Too short");

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var refused = await new AuthApiClient(staff).PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{created.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("not_publishable", refused.Error!.Code);

        var blockers = JobApi.Details(refused);
        Assert.Contains(blockers, error => error.Field == "description");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbContext.Jobs.AsNoTracking()
            .SingleAsync(item => item.Slug == slug);
        Assert.False(stored.IsPublished);

        var stillOpen = await dbContext.ContentSubmissions.AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);
        Assert.Equal(SubmissionStatus.Pending, stillOpen.Status);
        Assert.Null(stillOpen.ReviewedAt);
    }

    [Fact]
    public async Task Rejecting_leaves_the_draft_unpublished_and_the_author_can_correct_and_resend_it()
    {
        var email = TestData.UniqueEmail("sub-rejected");
        var slug = JobApi.NewSlug();
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(email));
        var created = await SubmissionApi.SubmitJobAsync(api, slug);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var rejected = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{created.Id}/reject",
            new { reviewNote = "The description reads like an advertisement, not a posting." });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        var decided = SubmissionApi.Read<SubmissionViews.QueueRow>(rejected);
        Assert.Equal(SubmissionStatus.Rejected, decided.Status);
        Assert.Equal(
            "The description reads like an advertisement, not a posting.", decided.ReviewNote);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stored = await dbContext.Jobs.AsNoTracking()
            .SingleAsync(item => item.Slug == slug);
        Assert.False(stored.IsPublished);

        using var reader = factory.CreateClient();
        using var hidden = await reader.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        // The author corrects the draft in place — same body the staff editor sends,
        // with the note travelling alongside it as part of the same replacement.
        var corrected = await api.PutForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}",
            new
            {
                kind = "Job",
                job = JobApi.Body(
                    slug,
                    organization: $"An employer about {slug}",
                    published: false,
                    title: "A posting rewritten after the turnaround"),
                note = "Rewritten, and the old wording was the problem.",
            });
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        var edited = SubmissionApi.Read<SubmissionViews.SubmissionView>(corrected);
        Assert.Equal("A posting rewritten after the turnaround", edited.Title);
        Assert.Equal("Rewritten, and the old wording was the problem.", edited.Note);
        Assert.Equal(SubmissionStatus.Rejected, edited.Status);

        // A body aimed at the wrong kind is refused rather than half-understood.
        var mismatched = await api.PutForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}",
            new { kind = "Event", @event = EventApi.Body(slug, "An event that is not this", published: false) });
        Assert.Equal(HttpStatusCode.BadRequest, mismatched.StatusCode);
        Assert.Equal("validation_failed", mismatched.Error!.Code);
        Assert.Contains(JobApi.Details(mismatched), error => error.Field == "kind");

        // Resending re-opens the question, with the old decision cleared away.
        var resent = await api.PostForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}/resubmit",
            body: null);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var reopened = SubmissionApi.Read<SubmissionViews.SubmissionView>(resent);
        Assert.Equal(SubmissionStatus.Pending, reopened.Status);
        Assert.Null(reopened.ReviewNote);
        Assert.Null(reopened.ReviewedAt);
        Assert.Equal("Rewritten, and the old wording was the problem.", reopened.Note);

        // Only a rejection can be resent: a pending one is already waiting.
        var again = await api.PostForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}/resubmit",
            body: null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("submission_not_rejected", again.Error!.Code);

        // And the corrected draft now passes: approval goes out live.
        var approved = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{created.Id}/approve",
            new { reviewNote = "Rewritten; reads like a posting now." });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using var afterApproval = factory.CreateClient();
        using var live = await afterApproval.GetAsync($"/api/jobs/{slug}");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Reading_a_draft_back_for_the_editor_gives_the_author_the_body_that_was_sent()
    {
        var email = TestData.UniqueEmail("sub-detail");
        var slug = JobApi.NewSlug();
        var api = new AuthApiClient(factory.CreateSignedInClientAsync(email));

        // Anonymous never gets a draft body; the fallback policy is the whole test.
        using var anonymous = factory.CreateClient();
        var refused = await anonymous.GetAsync($"{SubmissionApi.AccountPath}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        var created = await SubmissionApi.SubmitJobAsync(
            api,
            slug,
            note: "Written at the employer's own desk.",
            description: "Written once, and read back unchanged.");

        // The editor opens with what was actually sent — body, note and queue status —
        // beside exactly one kind's payload, so a wrong-kind form can never be built.
        var detail = await api.GetAsync<SubmissionViews.DetailView>(
            $"{SubmissionApi.AccountPath}/{created.Id}");
        Assert.Equal(created.Id, detail.Submission.Id);
        Assert.Equal("Written at the employer's own desk.", detail.Submission.Note);
        Assert.Equal(SubmissionStatus.Pending, detail.Submission.Status);
        Assert.NotNull(detail.Job);
        Assert.Equal(slug, detail.Job!.Slug);
        Assert.Equal("Written once, and read back unchanged.", detail.Job.Description);
        Assert.False(detail.Job.IsPublished);
        Assert.Null(detail.Job.DistrictCode);
        Assert.Null(detail.Event);
        Assert.Null(detail.Deal);

        // Somebody else's submission is unreadable, the same 404 as one that never existed.
        using var strangerClient = factory.CreateSignedInClientAsync(
            TestData.UniqueEmail("sub-detail-stranger"));
        using var strangerRead = await strangerClient.GetAsync(
            $"{SubmissionApi.AccountPath}/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, strangerRead.StatusCode);
    }

    [Fact]
    public async Task Events_and_offers_go_through_the_same_queue_with_the_same_decisions()
    {
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("sub-kinds")));

        var eventSlug = JobApi.NewSlug();
        var eventTitle = $"A gathering about {eventSlug}";
        var eventSnapshot = await api.PostForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/events",
            SubmissionApi.EventSubmission(eventSlug, eventTitle));
        Assert.Equal(HttpStatusCode.Created, eventSnapshot.StatusCode);
        var eventSubmission = SubmissionApi.Read<SubmissionViews.SubmissionView>(eventSnapshot);
        Assert.Equal(SubmissionKind.Event, eventSubmission.Kind);

        var dealSlug = DealApi.NewSlug();
        var dealTitle = $"An offer about {dealSlug}";
        var dealSnapshot = await api.PostForSnapshotAsync(
            $"{SubmissionApi.AccountPath}/deals",
            SubmissionApi.DealSubmission(dealSlug, dealTitle));
        Assert.Equal(HttpStatusCode.Created, dealSnapshot.StatusCode);
        var dealSubmission = SubmissionApi.Read<SubmissionViews.SubmissionView>(dealSnapshot);
        Assert.Equal(SubmissionKind.Deal, dealSubmission.Kind);

        // Each kind reads back through its own payload, with the linked district
        // resolved — the projection the author's editor prefills from.
        var eventDetail = await api.GetAsync<SubmissionViews.DetailView>(
            $"{SubmissionApi.AccountPath}/{eventSubmission.Id}");
        Assert.Equal(eventTitle, eventDetail.Event!.Title);
        Assert.Equal("bagmati-kathmandu", eventDetail.Event.DistrictCode);
        Assert.Null(eventDetail.Job);
        Assert.Null(eventDetail.Deal);

        var dealDetail = await api.GetAsync<SubmissionViews.DetailView>(
            $"{SubmissionApi.AccountPath}/{dealSubmission.Id}");
        Assert.Equal(dealTitle, dealDetail.Deal!.Title);
        Assert.Equal("bagmati-lalitpur", dealDetail.Deal.DistrictCode);
        Assert.Null(dealDetail.Job);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);

        // The kind filter narrows the queue, and both rows sit in it under their own kind.
        var all = await SubmissionApi.ReadQueueAsync(moderator, "?pageSize=100");
        Assert.Contains(all.Items, item => item.Id == eventSubmission.Id && item.Kind == SubmissionKind.Event);
        Assert.Contains(all.Items, item => item.Id == dealSubmission.Id && item.Kind == SubmissionKind.Deal);

        var eventsOnly = await SubmissionApi.ReadQueueAsync(moderator, "?kind=Event&pageSize=100");
        Assert.Contains(eventsOnly.Items, item => item.Id == eventSubmission.Id);
        Assert.DoesNotContain(eventsOnly.Items, item => item.Id == dealSubmission.Id);

        // Approving the event publishes the event and leaves the offer as a draft.
        var staffApi = new AuthApiClient(moderator);
        var approvedEvent = await staffApi.PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{eventSubmission.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approvedEvent.StatusCode);

        using var reader = factory.CreateClient();
        using var liveEvent = await reader.GetAsync($"/api/events/{eventSlug}");
        Assert.Equal(HttpStatusCode.OK, liveEvent.StatusCode);
        using var hiddenDeal = await reader.GetAsync($"/api/deals/{dealSlug}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenDeal.StatusCode);

        var approvedDeal = await staffApi.PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{dealSubmission.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approvedDeal.StatusCode);

        using var liveDeal = await reader.GetAsync($"/api/deals/{dealSlug}");
        Assert.Equal(HttpStatusCode.OK, liveDeal.StatusCode);
    }

    [Fact]
    public async Task The_dashboard_counts_this_queue_as_work_to_do()
    {
        var slug = JobApi.NewSlug();
        var api = new AuthApiClient(
            factory.CreateSignedInClientAsync(TestData.UniqueEmail("sub-dashboard")));
        var created = await SubmissionApi.SubmitJobAsync(api, slug);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(admin);

        var before = await adminApi.GetAsync<SubmissionViews.DashboardView>(
            "/api/admin/dashboard");
        Assert.True(before.NeedsAttention.PendingSubmissions >= 1);

        var approved = await adminApi.PostForSnapshotAsync(
            $"{SubmissionApi.QueuePath}/{created.Id}/approve",
            new { reviewNote = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var after = await adminApi.GetAsync<SubmissionViews.DashboardView>(
            "/api/admin/dashboard");
        Assert.Equal(
            before.NeedsAttention.PendingSubmissions - 1,
            after.NeedsAttention.PendingSubmissions);
    }
}
