using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Tests;

/// <summary>
/// The JSON shape the dashboard returns, with the two nested groups the frontend reads.
/// </summary>
internal static class Dashboard
{
    internal sealed record CountsView(
        int Users,
        int Businesses,
        int Jobs,
        int Deals,
        int Events,
        int StudentResources,
        int QuizQuestions,
        int Calculators);

    internal sealed record DraftsView(
        int Businesses,
        int Jobs,
        int Deals,
        int Events,
        int StudentResources,
        int QuizQuestions,
        int Calculators);

    internal sealed record StaleView(
        int Jobs,
        int Deals,
        int StudentResources,
        int Events);

    internal sealed record AttentionView(
        DraftsView Drafts,
        StaleView StaleWhilePublished,
        int UnverifiedBusinesses);

    internal sealed record SummaryView(CountsView Counts, AttentionView NeedsAttention);
}

/// <summary>
/// The staff dashboard: one staff-only read of every module's totals and of the rows a staff
/// member can act on.
///
/// <para>
/// The counts are asserted twice over — against each management page's own <c>totalCount</c>,
/// which is the number the dashboard exists to agree with, and against the tables themselves for
/// the attention rows, so a section that reports "3 drafts" is checked against three rows rather
/// than only against its own arithmetic.
/// </para>
/// </summary>
public class StaffDashboardTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string Path = "/api/admin/dashboard";
    private const string JobsAdminPath = "/api/admin/jobs";

    [Fact]
    public async Task An_anonymous_reader_is_refused()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_reader_without_the_staff_role_is_refused()
    {
        using var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("reader"));

        using var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Moderator)]
    public async Task Either_staff_role_can_read_the_dashboard(string role)
    {
        using var client = await factory.CreateStaffClientAsync(role);
        var api = new AuthApiClient(client);

        var summary = await api.GetAsync<Dashboard.SummaryView>(Path);

        AssertAllNonNegative(summary);
    }

    [Fact]
    public async Task Every_count_is_the_total_the_management_page_reports()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var summary = await api.GetAsync<Dashboard.SummaryView>(Path);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(await dbContext.Users.CountAsync(), summary.Counts.Users);
        Assert.Equal(await StaffTotalAsync(api, "/api/admin/businesses"), summary.Counts.Businesses);
        Assert.Equal(await StaffTotalAsync(api, "/api/admin/jobs"), summary.Counts.Jobs);
        Assert.Equal(await StaffTotalAsync(api, "/api/admin/deals"), summary.Counts.Deals);
        Assert.Equal(await StaffTotalAsync(api, "/api/admin/events"), summary.Counts.Events);
        Assert.Equal(
            await StaffTotalAsync(api, "/api/admin/students/resources"),
            summary.Counts.StudentResources);
        Assert.Equal(
            await StaffTotalAsync(api, "/api/admin/quiz/questions"),
            summary.Counts.QuizQuestions);
        Assert.Equal(
            await StaffTotalAsync(api, "/api/admin/calculators"),
            summary.Counts.Calculators);
    }

    [Fact]
    public async Task The_draft_counts_are_the_unpublished_rows_in_the_tables()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var summary = await api.GetAsync<Dashboard.SummaryView>(Path);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var drafts = summary.NeedsAttention.Drafts;

        Assert.Equal(await dbContext.Businesses.CountAsync(row => !row.IsPublished), drafts.Businesses);
        Assert.Equal(await dbContext.Jobs.CountAsync(row => !row.IsPublished), drafts.Jobs);
        Assert.Equal(await dbContext.Deals.CountAsync(row => !row.IsPublished), drafts.Deals);
        Assert.Equal(await dbContext.Events.CountAsync(row => !row.IsPublished), drafts.Events);
        Assert.Equal(
            await dbContext.StudentResources.CountAsync(row => !row.IsPublished),
            drafts.StudentResources);
        Assert.Equal(
            await dbContext.QuizQuestions.CountAsync(row => !row.IsPublished),
            drafts.QuizQuestions);
        Assert.Equal(
            await dbContext.Calculators.CountAsync(row => !row.IsPublished),
            drafts.Calculators);
    }

    [Fact]
    public async Task The_stale_counts_are_the_published_rows_the_public_scope_no_longer_serves()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var summary = await api.GetAsync<Dashboard.SummaryView>(Path);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dates = scope.ServiceProvider.GetRequiredService<INepalDateProvider>();
        var stale = summary.NeedsAttention.StaleWhilePublished;
        var today = dates.Today;

        // Published minus visible, worked out here the way the endpoint works it out, so the
        // section is checked against the visibility rule itself rather than against a second
        // copy of the date comparison written for the test.
        Assert.Equal(
            await dbContext.Jobs.CountAsync(row => row.IsPublished)
                - await PublicContentScope.Jobs(dbContext.Jobs, today).CountAsync(),
            stale.Jobs);
        Assert.Equal(
            await dbContext.Deals.CountAsync(row => row.IsPublished)
                - await PublicContentScope.Deals(dbContext.Deals, today).CountAsync(),
            stale.Deals);
        Assert.Equal(
            await dbContext.StudentResources.CountAsync(row => row.IsPublished)
                - await PublicContentScope.StudentResources(dbContext.StudentResources, today)
                    .CountAsync(),
            stale.StudentResources);
        Assert.Equal(
            await dbContext.Events.CountAsync(row => row.IsPublished)
                - await PublicContentScope.Events(dbContext.Events, dates.NepalNow).CountAsync(),
            stale.Events);

        Assert.Equal(
            await dbContext.Businesses.CountAsync(
                business => business.IsPublished && !business.IsVerified),
            summary.NeedsAttention.UnverifiedBusinesses);
    }

    [Fact]
    public async Task The_seeded_catalogue_actually_has_something_to_report()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var summary = await api.GetAsync<Dashboard.SummaryView>(Path);

        // A dashboard that always renders an empty "needs attention" because every count was
        // wired to zero would pass every assertion above. The seed set ships drafts, closed
        // postings and an expired offer on purpose, so at least one row must be reported — and
        // the totals are non-trivial, which rules out a response built from empty tables.
        Assert.True(summary.Counts.Jobs > 0, "The seeded job board was expected to have rows.");
        Assert.True(summary.Counts.Calculators > 0, "The seeded calculators were expected.");

        var reported = DashboardIsReportingSomething(summary);
        Assert.True(
            reported,
            "The seeded catalogue has drafts and closed rows, so the attention section must report at least one.");
    }

    [Fact]
    public async Task Publishing_a_draft_moves_it_out_of_the_attention_list_and_leaves_the_total_alone()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        var before = await api.GetAsync<Dashboard.SummaryView>(Path);
        var organization = $"Dashboard Employer {Guid.NewGuid():N}";
        var slug = JobApi.NewSlug();

        var created = await api.PostForSnapshotAsync(
            JobsAdminPath,
            JobApi.Body(slug, organization, published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var job = JobApi.ReadCreated(created);

        var asDraft = await api.GetAsync<Dashboard.SummaryView>(Path);
        Assert.Equal(before.Counts.Jobs + 1, asDraft.Counts.Jobs);
        Assert.Equal(before.NeedsAttention.Drafts.Jobs + 1, asDraft.NeedsAttention.Drafts.Jobs);
        Assert.Equal(
            before.NeedsAttention.StaleWhilePublished.Jobs,
            asDraft.NeedsAttention.StaleWhilePublished.Jobs);

        var published = await api.PutForSnapshotAsync(
            $"{JobsAdminPath}/{job.Id}",
            JobApi.Body(slug, organization, published: true));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var asPublished = await api.GetAsync<Dashboard.SummaryView>(Path);
        Assert.Equal(before.Counts.Jobs + 1, asPublished.Counts.Jobs);
        Assert.Equal(before.NeedsAttention.Drafts.Jobs, asPublished.NeedsAttention.Drafts.Jobs);
        Assert.Equal(
            before.NeedsAttention.StaleWhilePublished.Jobs,
            asPublished.NeedsAttention.StaleWhilePublished.Jobs);

        // Put the table back the way it was found, so the assertions above mean the same thing
        // for the next test as they did for this one. Retract first: only a draft can be deleted.
        var retracted = await api.PutForSnapshotAsync(
            $"{JobsAdminPath}/{job.Id}",
            JobApi.Body(slug, organization, published: false));
        Assert.Equal(HttpStatusCode.OK, retracted.StatusCode);

        var deleted = await api.DeleteForSnapshotAsync($"{JobsAdminPath}/{job.Id}");
        Assert.True(
            deleted.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Deleting the draft gave {(int)deleted.StatusCode}.");

        var after = await api.GetAsync<Dashboard.SummaryView>(Path);
        Assert.Equal(before.Counts.Jobs, after.Counts.Jobs);
        Assert.Equal(before.NeedsAttention.Drafts.Jobs, after.NeedsAttention.Drafts.Jobs);
    }

    private static bool DashboardIsReportingSomething(Dashboard.SummaryView summary)
    {
        var drafts = summary.NeedsAttention.Drafts;
        var stale = summary.NeedsAttention.StaleWhilePublished;

        return drafts.Businesses > 0
            || drafts.Jobs > 0
            || drafts.Deals > 0
            || drafts.Events > 0
            || drafts.StudentResources > 0
            || drafts.QuizQuestions > 0
            || drafts.Calculators > 0
            || stale.Jobs > 0
            || stale.Deals > 0
            || stale.StudentResources > 0
            || stale.Events > 0
            || summary.NeedsAttention.UnverifiedBusinesses > 0;
    }

    private static void AssertAllNonNegative(Dashboard.SummaryView summary)
    {
        var counts = summary.Counts;
        var drafts = summary.NeedsAttention.Drafts;
        var stale = summary.NeedsAttention.StaleWhilePublished;

        int[] values =
        [
            counts.Users, counts.Businesses, counts.Jobs, counts.Deals, counts.Events,
            counts.StudentResources, counts.QuizQuestions, counts.Calculators,
            drafts.Businesses, drafts.Jobs, drafts.Deals, drafts.Events, drafts.StudentResources,
            drafts.QuizQuestions, drafts.Calculators,
            stale.Jobs, stale.Deals, stale.StudentResources, stale.Events,
            summary.NeedsAttention.UnverifiedBusinesses,
        ];

        Assert.All(values, value => Assert.True(value >= 0, $"A dashboard count was {value}."));
    }

    /// <summary>The number a management page puts in its own "1–20 of N" line.</summary>
    private static async Task<int> StaffTotalAsync(AuthApiClient api, string path)
    {
        using var response = await api.GetRawAsync($"{path}?page=1&pageSize=1");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32();
    }
}
