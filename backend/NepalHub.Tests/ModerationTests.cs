using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Tests;

/// <summary>
/// The JSON shapes the report endpoints return. Enums travel by name, which the shared
/// test options read back for us.
/// </summary>
internal static class Moderations
{
    internal sealed record ReportView(
        Guid Id,
        ReportTargetType TargetType,
        Guid TargetId,
        ReportReason Reason,
        ReportStatus Status,
        DateTime CreatedAt);

    internal sealed record QueueRowView(
        Guid Id,
        ReportTargetType TargetType,
        Guid TargetId,
        string? TargetTitle,
        string? TargetSlug,
        string ReporterName,
        ReportReason Reason,
        string? Note,
        ReportStatus Status,
        DateTime CreatedAt,
        string? ResolvedByName,
        DateTime? ResolvedAt,
        string? ResolutionNote);

    internal sealed record AuditRowView(
        Guid Id,
        string Action,
        string TargetType,
        string TargetId,
        string? AdminName,
        string? Before,
        string? After,
        string? Ip,
        DateTime CreatedAt);

    internal sealed record PageView<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasPrevious,
        bool HasNext);

    /// <summary>An id from the seeded catalogue that a member is currently allowed to see.</summary>
    internal static async Task<Guid> FirstVisibleTargetAsync(
        IServiceProvider services, ReportTargetType targetType)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dates = scope.ServiceProvider.GetRequiredService<INepalDateProvider>();

        return targetType switch
        {
            ReportTargetType.Job => await PublicContentScope
                .Jobs(dbContext.Jobs, dates.Today)
                .Select(job => job.Id)
                .FirstAsync(),

            ReportTargetType.Business => await PublicContentScope
                .Businesses(dbContext.Businesses)
                .Select(business => business.Id)
                .FirstAsync(),

            ReportTargetType.Deal => await PublicContentScope
                .Deals(dbContext.Deals, dates.Today)
                .Select(deal => deal.Id)
                .FirstAsync(),

            ReportTargetType.Event => await PublicContentScope
                .Events(dbContext.Events, dates.NepalNow)
                .Select(calendarEvent => calendarEvent.Id)
                .FirstAsync(),

            _ => await PublicContentScope
                .StudentResources(dbContext.StudentResources, dates.Today)
                .Select(resource => resource.Id)
                .FirstAsync(),
        };
    }

    /// <summary>The seeded job a report test can point at, with its title for the queue assertion.</summary>
    internal static async Task<(Guid Id, string Title)> FirstVisibleJobAsync(
        IServiceProvider services, int skip = 0)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dates = scope.ServiceProvider.GetRequiredService<INepalDateProvider>();

        var job = await PublicContentScope
            .Jobs(dbContext.Jobs, dates.Today)
            .OrderBy(candidate => candidate.Id)
            .Skip(skip)
            .Select(candidate => new { candidate.Id, candidate.Title })
            .FirstAsync();

        return (job.Id, job.Title);
    }

    /// <summary>
    /// A copy of a seeded posting that has never been published, for the assertion that
    /// an invisible target is answered the same way as one that does not exist. The copy
    /// rather than an edit: this database is shared, and a seeded row another test counts
    /// must not change underneath it.
    /// </summary>
    internal static async Task<Guid> SeedUnpublishedJobAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var source = await dbContext.Jobs.AsNoTracking().FirstAsync();

        var draft = new Job
        {
            Id = Guid.NewGuid(),
            Slug = $"unreportable-{Guid.NewGuid():N}",
            Title = source.Title,
            Organization = source.Organization,
            Description = source.Description,
            Category = source.Category,
            EmploymentType = source.EmploymentType,
            WorkMode = source.WorkMode,
            DistrictId = source.DistrictId,
            ApplicationUrl = source.ApplicationUrl,
            ApplicationDeadline = source.ApplicationDeadline,
            IsPublished = false,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
        };

        dbContext.Jobs.Add(draft);
        await dbContext.SaveChangesAsync();

        return draft.Id;
    }

    internal static async Task<Report> ReadReportAsync(IServiceProvider services, Guid reportId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var report = await dbContext.Reports
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == reportId);

        Assert.NotNull(report);

        return report!;
    }

    internal static async Task<int> ReportCountAsync(
        IServiceProvider services, string reporterEmail, Guid targetId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var reporter = await userManager.FindByEmailAsync(reporterEmail);
        Assert.NotNull(reporter);

        return await dbContext.Reports.CountAsync(
            report => report.ReporterUserId == reporter!.Id
                && report.TargetId == targetId);
    }

    internal static async Task<ApiEnvelope<ReportView>> ReadOwnReportAsync(
        HttpClient client, Guid reportId)
    {
        using var response = await client.GetAsync($"/api/reports/{reportId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<ReportView>>(TestJson.Options);

        Assert.NotNull(envelope);

        return envelope!;
    }

    internal static object ReportBody(ReportTargetType targetType, Guid targetId, string note = "") =>
        new
        {
            targetType = targetType.ToString(),
            targetId,
            reason = "Spam",
            note,
        };

    internal static async Task<ApiEnvelope<PageView<QueueRowView>>> ReadQueueAsync(
        HttpClient client, string query = "")
    {
        using var response = await client.GetAsync($"/api/admin/reports{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<PageView<QueueRowView>>>(TestJson.Options);

        Assert.NotNull(envelope);

        return envelope!;
    }

    internal static async Task<ApiEnvelope<PageView<AuditRowView>>> ReadAuditAsync(
        HttpClient client, string query = "")
    {
        using var response = await client.GetAsync($"/api/admin/audit-log{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<PageView<AuditRowView>>>(TestJson.Options);

        Assert.NotNull(envelope);

        return envelope!;
    }
}

/// <summary>
/// Filing a report as a member. Every test signs in of its own accord, because a report
/// without an account behind it is the one thing this endpoint refuses outright.
/// </summary>
public sealed class ReportFilingTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string Path = "/api/reports";

    [Fact]
    public async Task Filing_a_report_requires_an_account()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        using var response = await api.PostAsync(Path, new
        {
            targetType = "Job",
            targetId = Guid.NewGuid(),
            reason = "Spam",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Every_visible_listing_type_can_be_reported_and_read_back()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("reporter"));
        var api = new AuthApiClient(client);

        foreach (ReportTargetType targetType in Enum.GetValues<ReportTargetType>())
        {
            var targetId = await Moderations.FirstVisibleTargetAsync(factory.Services, targetType);

            using var created = await api.PostAsync(
                Path, Moderations.ReportBody(targetType, targetId));

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var envelope = await created.Content
                .ReadFromJsonAsync<ApiEnvelope<Moderations.ReportView>>(TestJson.Options);
            Assert.NotNull(envelope);
            Assert.Equal(targetType, envelope!.Data!.TargetType);
            Assert.Equal(targetId, envelope.Data.TargetId);
            Assert.Equal(ReportStatus.Open, envelope.Data.Status);

            var own = await Moderations.ReadOwnReportAsync(client, envelope.Data.Id);
            Assert.Equal(targetId, own.Data!.TargetId);
        }
    }

    [Fact]
    public async Task A_listing_that_is_not_visible_is_refused_like_one_that_does_not_exist()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("reporter"));
        var api = new AuthApiClient(client);

        var unpublished = await Moderations.SeedUnpublishedJobAsync(factory.Services);

        var invisible = await api.PostForSnapshotAsync(
            Path, Moderations.ReportBody(ReportTargetType.Job, unpublished));

        var missing = await api.PostForSnapshotAsync(
            Path, Moderations.ReportBody(ReportTargetType.Job, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
        Assert.Equal("target_not_found", invisible.Error!.Code);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("target_not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task The_same_member_cannot_report_the_same_listing_twice()
    {
        var email = TestData.UniqueEmail("reporter");
        var client = factory.CreateSignedInClientAsync(email);
        var api = new AuthApiClient(client);

        var targetId = await Moderations.FirstVisibleTargetAsync(factory.Services, ReportTargetType.Job);

        using var first = await api.PostAsync(Path, Moderations.ReportBody(ReportTargetType.Job, targetId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await api.PostForSnapshotAsync(
            Path, Moderations.ReportBody(ReportTargetType.Job, targetId));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("already_reported", second.Error!.Code);
        Assert.Equal(1, await Moderations.ReportCountAsync(factory.Services, email, targetId));
    }

    [Fact]
    public async Task A_request_without_a_target_or_a_reason_is_refused()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("reporter"));
        var api = new AuthApiClient(client);

        var response = await api.PostForSnapshotAsync(Path, new { note = "something" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", response.Error!.Code);

        var details = response.Error.Details?.ToString() ?? string.Empty;
        Assert.Contains("targetType", details);
        Assert.Contains("reason", details);
    }

    [Fact]
    public async Task A_note_longer_than_five_hundred_characters_is_refused()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("reporter"));
        var api = new AuthApiClient(client);

        var targetId = await Moderations.FirstVisibleTargetAsync(factory.Services, ReportTargetType.Job);

        var response = await api.PostForSnapshotAsync(
            Path,
            new
            {
                targetType = "Job",
                targetId,
                reason = "Spam",
                note = new string('x', 501),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", response.Error!.Code);
        Assert.Contains("note", response.Error.Details?.ToString() ?? string.Empty);
    }
}

/// <summary>
/// The staff queue and the audit trail behind it. Reports are filed by a member of their
/// own and closed by staff of theirs, so the assertions are always about this test's rows
/// rather than about whatever else the shared queue happens to hold.
/// </summary>
public sealed class ReportQueueTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string QueuePath = "/api/admin/reports";
    private const string AuditPath = "/api/admin/audit-log";

    [Fact]
    public async Task The_queue_is_for_staff_and_reads_back_the_target_it_points_at()
    {
        var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("queue"));
        var memberApi = new AuthApiClient(member);

        var (jobId, jobTitle) = await Moderations.FirstVisibleJobAsync(factory.Services);
        var report = await FileAsync(memberApi, ReportTargetType.Job, jobId);

        using var refused = await member.GetAsync(QueuePath);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var queue = await Moderations.ReadQueueAsync(moderator, "?pageSize=100");

        var row = Assert.Single(queue.Data!.Items, candidate => candidate.Id == report.Id);
        Assert.Equal(jobTitle, row.TargetTitle);
        Assert.Equal(jobId, row.TargetId);
        Assert.Equal(ReportStatus.Open, row.Status);
        Assert.Equal("Test User", row.ReporterName);
    }

    [Fact]
    public async Task Open_reports_come_first_and_newest_before_oldest()
    {
        var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("queue"));
        var memberApi = new AuthApiClient(member);

        var firstJob = await Moderations.FirstVisibleJobAsync(factory.Services);
        var secondJob = await Moderations.FirstVisibleJobAsync(factory.Services, skip: 1);

        // Two different listings, so both are open and only the clock separates them.
        var older = await FileAsync(memberApi, ReportTargetType.Job, firstJob.Id);
        var newer = await FileAsync(memberApi, ReportTargetType.Job, secondJob.Id);

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var queue = await Moderations.ReadQueueAsync(staff, "?pageSize=100");

        var items = queue.Data!.Items;
        var olderIndex = items.ToList().FindIndex(candidate => candidate.Id == older.Id);
        var newerIndex = items.ToList().FindIndex(candidate => candidate.Id == newer.Id);

        Assert.True(olderIndex >= 0, "the older report should be in the queue");
        Assert.True(newerIndex >= 0, "the newer report should be in the queue");
        Assert.True(newerIndex < olderIndex, "the newer report should be listed first");

        using var nonsense = await staff.GetAsync($"{QueuePath}?status=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
        var body = await nonsense.Content.ReadAsStringAsync();
        Assert.Contains("status_invalid", body);
    }

    [Fact]
    public async Task Closing_a_report_records_it_for_the_member_and_in_the_audit_log()
    {
        var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("queue"));
        var memberApi = new AuthApiClient(member);

        var jobId = await Moderations.FirstVisibleTargetAsync(factory.Services, ReportTargetType.Job);
        var report = await FileAsync(memberApi, ReportTargetType.Job, jobId);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var moderatorApi = new AuthApiClient(moderator);

        using var resolved = await moderatorApi.PostAsync(
            $"{QueuePath}/{report.Id}/resolve",
            new { resolutionNote = "Removed the listing." });

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var stored = await Moderations.ReadReportAsync(factory.Services, report.Id);
        Assert.Equal(ReportStatus.Resolved, stored.Status);
        Assert.Equal("Removed the listing.", stored.ResolutionNote);
        Assert.NotNull(stored.ResolvedAt);

        var session = await moderatorApi.GetAsync<SessionResponse>("/api/auth/session");
        Assert.Equal(session.User!.Id, stored.ResolvedByUserId);

        var own = await Moderations.ReadOwnReportAsync(member, report.Id);
        Assert.Equal(ReportStatus.Resolved, own.Data!.Status);

        // A moderator wrote the log, but only an admin may read it.
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var audit = await Moderations.ReadAuditAsync(admin, "?pageSize=100");

        var entry = Assert.Single(
            audit.Data!.Items,
            candidate => candidate.TargetId == report.Id.ToString());
        Assert.Equal("report.resolved", entry.Action);
        Assert.Equal("report", entry.TargetType);
        Assert.Contains("Open", entry.Before);
        Assert.Contains("Resolved", entry.After);

        var filtered = await Moderations.ReadAuditAsync(admin, "?action=report.resolved");
        Assert.Contains(filtered.Data!.Items, candidate => candidate.TargetId == report.Id.ToString());

        var otherAction = await Moderations.ReadAuditAsync(admin, "?action=user.deactivated");
        Assert.DoesNotContain(otherAction.Data!.Items, candidate => candidate.TargetId == report.Id.ToString());

        // The queue reflects it, and the status filter is how staff page through the
        // work they have already done.
        var open = await Moderations.ReadQueueAsync(moderator, "?status=Open&pageSize=100");
        Assert.DoesNotContain(open.Data!.Items, candidate => candidate.Id == report.Id);

        var resolvedQueue = await Moderations.ReadQueueAsync(moderator, "?status=Resolved&pageSize=100");
        Assert.Contains(resolvedQueue.Data!.Items, candidate => candidate.Id == report.Id);

        using var moderator2 = await factory.CreateStaffClientAsync(Roles.Moderator);
        var again = await new AuthApiClient(moderator2).PostForSnapshotAsync(
            $"{QueuePath}/{report.Id}/dismiss", new { resolutionNote = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("report_closed", again.Error!.Code);
    }

    [Fact]
    public async Task The_audit_log_is_admin_only()
    {
        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);

        using var response = await moderator.GetAsync(AuditPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Files one report and returns it, as the member sees it.</summary>
    private static async Task<Moderations.ReportView> FileAsync(
        AuthApiClient member, ReportTargetType targetType, Guid targetId)
    {
        using var created = await member.PostAsync(
            "/api/reports", Moderations.ReportBody(targetType, targetId));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var envelope = await created.Content
            .ReadFromJsonAsync<ApiEnvelope<Moderations.ReportView>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }
}
