using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Businesses;
using NepalHub.Api.Features.Calculators;
using NepalHub.Api.Features.Deals;
using NepalHub.Api.Features.Events;
using NepalHub.Api.Features.Jobs;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Students;

namespace NepalHub.Api.Features.Admin;

/// <summary>
/// One read of the whole catalogue for the staff dashboard.
///
/// <para>
/// Every number here comes from a table the management pages already read. The eight headline
/// counts are the <c>TotalCount</c> of each module's own staff list, asked for with no filters, so
/// a card that says "16 jobs" and the job board's "1–20 of 16" are the same query rather than two
/// queries that agree today. Nothing is estimated, cached or invented.
/// </para>
///
/// <para>
/// "Needs attention" is the same rule: only rows a staff member can act on. Drafts are the rows
/// nobody has published yet. "Stale while published" is a row whose publish flag still says live
/// while <see cref="PublicContentScope"/> no longer serves it to a reader — a job past its
/// deadline, an offer past its last day, a resource past its expiry, an event that has finished.
/// The second half is measured as <c>published − visible</c> through the scope itself rather than
/// by restating the date comparison here, because a visibility rule written twice is a rule that
/// eventually changes in only one of them.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class StaffDashboardController(
    AppDbContext dbContext,
    INepalDateProvider dates,
    CalculatorService calculatorService,
    JobService jobService,
    DealService dealService,
    EventService eventService,
    BusinessService businessService,
    StudentResourceService studentResourceService,
    QuizService quizService) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType<ApiResponse<DashboardSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<DashboardSummary>>> Get(
        CancellationToken cancellationToken)
    {
        // Page one of one row: the staff lists answer with their own TotalCount, and the single
        // projected row they also fetch is the price of not duplicating their counting rules.
        var paging = PageRequest.Normalise(1, 1);

        var today = dates.Today;
        var now = dates.NepalNow;

        var users = await dbContext.Users.CountAsync(cancellationToken);

        var businesses = await businessService.ListForAdminAsync(
            paging, search: null, provinceId: null, districtId: null, category: null,
            verified: null, filter: BusinessAdminFilter.All, cancellationToken);
        var jobs = await jobService.ListForAdminAsync(
            paging, search: null, provinceId: null, districtId: null, category: null,
            employmentType: null, workMode: null, filter: JobAdminFilter.All, cancellationToken);
        var deals = await dealService.ListForAdminAsync(
            paging, search: null, provinceId: null, districtId: null,
            filter: DealAdminFilter.All, cancellationToken);
        var events = await eventService.ListForAdminAsync(
            paging, search: null, provinceId: null, districtId: null, category: null,
            filter: EventAdminFilter.All, cancellationToken);
        var studentResources = await studentResourceService.ListForAdminAsync(
            paging, search: null, type: null, category: null, level: null,
            filter: ResourceAdminFilter.All, cancellationToken);
        var quizQuestions = await quizService.ListForAdminAsync(
            paging, search: null, category: null, cancellationToken);
        var calculators = await calculatorService.ListForAdminAsync(
            paging, search: null, cancellationToken);

        var counts = new DashboardCounts(
            Users: users,
            Businesses: businesses.TotalCount,
            Jobs: jobs.TotalCount,
            Deals: deals.TotalCount,
            Events: events.TotalCount,
            StudentResources: studentResources.TotalCount,
            QuizQuestions: quizQuestions.TotalCount,
            Calculators: calculators.TotalCount);

        var publishedBusinesses = await dbContext.Businesses
            .CountAsync(business => business.IsPublished, cancellationToken);
        var publishedJobs = await dbContext.Jobs
            .CountAsync(job => job.IsPublished, cancellationToken);
        var publishedDeals = await dbContext.Deals
            .CountAsync(deal => deal.IsPublished, cancellationToken);
        var publishedEvents = await dbContext.Events
            .CountAsync(calendarEvent => calendarEvent.IsPublished, cancellationToken);
        var publishedStudentResources = await dbContext.StudentResources
            .CountAsync(resource => resource.IsPublished, cancellationToken);
        var publishedQuizQuestions = await dbContext.QuizQuestions
            .CountAsync(question => question.IsPublished, cancellationToken);
        var publishedCalculators = await dbContext.Calculators
            .CountAsync(calculator => calculator.IsPublished, cancellationToken);

        var visibleJobs = await PublicContentScope.Jobs(dbContext.Jobs, today)
            .CountAsync(cancellationToken);
        var visibleDeals = await PublicContentScope.Deals(dbContext.Deals, today)
            .CountAsync(cancellationToken);
        var visibleStudentResources = await PublicContentScope.StudentResources(
                dbContext.StudentResources, today)
            .CountAsync(cancellationToken);
        var visibleEvents = await PublicContentScope.Events(dbContext.Events, now)
            .CountAsync(cancellationToken);

        var attention = new DashboardAttention(
            Drafts: new DraftCounts(
                Businesses: businesses.TotalCount - publishedBusinesses,
                Jobs: jobs.TotalCount - publishedJobs,
                Deals: deals.TotalCount - publishedDeals,
                Events: events.TotalCount - publishedEvents,
                StudentResources: studentResources.TotalCount - publishedStudentResources,
                QuizQuestions: quizQuestions.TotalCount - publishedQuizQuestions,
                Calculators: calculators.TotalCount - publishedCalculators),
            StaleWhilePublished: new StaleCounts(
                Jobs: publishedJobs - visibleJobs,
                Deals: publishedDeals - visibleDeals,
                StudentResources: publishedStudentResources - visibleStudentResources,
                Events: publishedEvents - visibleEvents),
            // Unverifying does not retract a listing, so a live row can lose its badge while
            // staying public. That is the one live row a reader is being told to trust and a
            // staff member has stopped standing behind.
            UnverifiedBusinesses: await dbContext.Businesses
                .CountAsync(business => business.IsPublished && !business.IsVerified,
                    cancellationToken));

        return Ok(ApiResponse<DashboardSummary>.Ok(new DashboardSummary(counts, attention)));
    }
}

/// <summary>How many rows each module holds, drafts included — the same number its staff list reports.</summary>
public sealed record DashboardCounts(
    int Users,
    int Businesses,
    int Jobs,
    int Deals,
    int Events,
    int StudentResources,
    int QuizQuestions,
    int Calculators);

/// <summary>Unpublished rows, per module. Users have no publish state, so they have none here.</summary>
public sealed record DraftCounts(
    int Businesses,
    int Jobs,
    int Deals,
    int Events,
    int StudentResources,
    int QuizQuestions,
    int Calculators);

/// <summary>
/// Rows still flagged published that <see cref="PublicContentScope"/> no longer serves. Measured
/// through the scope, so "stale" means exactly what "not visible to a reader" means.
/// </summary>
public sealed record StaleCounts(
    int Jobs,
    int Deals,
    int StudentResources,
    int Events);

public sealed record DashboardAttention(
    DraftCounts Drafts,
    StaleCounts StaleWhilePublished,
    int UnverifiedBusinesses);

public sealed record DashboardSummary(
    DashboardCounts Counts,
    DashboardAttention NeedsAttention);
