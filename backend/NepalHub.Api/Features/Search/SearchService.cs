using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Search;

/// <summary>
/// One row of public content, reduced to the text that decides its rank plus the fields a result
/// line shows.
/// </summary>
/// <remarks>
/// The haystacks are kept as plain strings so the ranker never needs to know which module a row came
/// from, and the response item is carried alongside rather than derived from them: the ranker reads
/// one shape, the client reads the other.
/// </remarks>
internal sealed record SearchCandidate(
    SearchModule Module,
    string TitleHaystack,
    string MetaHaystack,
    string SummaryHaystack,
    string BodyHaystack,
    DateTime CreatedAt,
    SearchResultItem Item)
{
    /// <summary>Which relevance tier this row matched, or -1 for no match. Filled in after projection.</summary>
    public int Rank { get; init; }
}

/// <summary>
/// One module's share of a search: the rows that ranked, plus whether its SQL query filled the
/// candidate cap.
/// </summary>
/// <remarks>
/// <see cref="HitCap"/> is measured on the rows SQL returned, not on the rows that survived
/// ranking. Ranking drops every row that matched only in a low-value haystack, so a module that
/// returned a full cap but ranked just under it has still hidden matches from the page and must be
/// reported as truncated. Measuring after ranking would under-report truncation exactly in the case
/// the flag exists to warn about.
/// </remarks>
internal sealed record ModuleCandidates(List<SearchCandidate> Candidates, bool HitCap)
{
    /// <summary>The share for a module the caller filtered out, or one that matched nothing.</summary>
    public static ModuleCandidates Empty { get; } = new([], false);
}

/// <summary>
/// One keyword search across every public module.
///
/// <para>
/// The problem is that six tables with six different column lists have to produce one ranked page.
/// They cannot be unioned in SQL — there is no shared key to union on, and each source projects a
/// different set of columns — so the work is done in three steps: take a bounded number of candidates
/// from each source, rank them all together, then page the merged result.
/// </para>
///
/// <para>
/// The bound is what makes this affordable. Each source contributes at most
/// <see cref="SearchLimits.CandidateCap"/> candidates and no source is counted, because a
/// <c>COUNT</c> over the whole matched set is the one query here that grows without limit as the
/// catalogue does. A request therefore touches at most six bounded, indexed queries.
/// </para>
///
/// <para>
/// The queries run one after another rather than concurrently. An <see cref="AppDbContext"/> is not
/// thread-safe, so running six at once would throw rather than save any real time at this size.
/// </para>
/// </summary>
public sealed class SearchService(AppDbContext dbContext, INepalDateProvider dates)
{
    /// <summary>
    /// The order groups appear in. Fixed rather than by count, so a page does not reshuffle the
    /// modules when two of them happen to return the same number of hits.
    /// </summary>
    private static readonly SearchModule[] ModuleOrder =
    [
        SearchModule.Students,
        SearchModule.Jobs,
        SearchModule.Businesses,
        SearchModule.Deals,
        SearchModule.Events,
        SearchModule.Calculators,
    ];

    private static readonly Dictionary<SearchModule, string> ModuleLabels = new()
    {
        [SearchModule.Students] = "Student Hub",
        [SearchModule.Jobs] = "Jobs",
        [SearchModule.Businesses] = "Businesses",
        [SearchModule.Deals] = "Offers",
        [SearchModule.Events] = "Events",
        [SearchModule.Calculators] = "Tools",
    };

    internal async Task<SearchResponse> SearchAsync(
        string preparedTerm,
        PageRequest paging,
        SearchModule module,
        CancellationToken cancellationToken)
    {
        var normalisedTerm = SearchText.Normalise(preparedTerm);
        var pattern = LikePattern.Contains(preparedTerm);
        var today = dates.Today;
        var now = dates.NepalNow;

        var perModule = new Dictionary<SearchModule, ModuleCandidates>
        {
            [SearchModule.Students] = module is SearchModule.All or SearchModule.Students
                ? await StudentsAsync(pattern, normalisedTerm, today, cancellationToken)
                : ModuleCandidates.Empty,
            [SearchModule.Jobs] = module is SearchModule.All or SearchModule.Jobs
                ? await JobsAsync(pattern, normalisedTerm, today, cancellationToken)
                : ModuleCandidates.Empty,
            [SearchModule.Businesses] = module is SearchModule.All or SearchModule.Businesses
                ? await BusinessesAsync(pattern, normalisedTerm, cancellationToken)
                : ModuleCandidates.Empty,
            [SearchModule.Deals] = module is SearchModule.All or SearchModule.Deals
                ? await DealsAsync(pattern, normalisedTerm, today, cancellationToken)
                : ModuleCandidates.Empty,
            [SearchModule.Events] = module is SearchModule.All or SearchModule.Events
                ? await EventsAsync(pattern, normalisedTerm, now, cancellationToken)
                : ModuleCandidates.Empty,
            [SearchModule.Calculators] = module is SearchModule.All or SearchModule.Calculators
                ? await CalculatorsAsync(pattern, normalisedTerm, cancellationToken)
                : ModuleCandidates.Empty,
        };

        // True when a source filled its cap, which is the only case where the totals below describe
        // what was shown rather than everything that matched.
        var truncated = perModule.Values.Any(batch => batch.HitCap);

        var ranked = perModule.Values
            .SelectMany(batch => batch.Candidates)
            .OrderBy(candidate => candidate.Rank)
            .ThenByDescending(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Item.Module)
            .ThenBy(candidate => candidate.Item.Slug, StringComparer.Ordinal)
            .ToList();

        // The candidate pool is the total, so the page is clamped against it for the same reason the
        // list endpoints clamp against their count: a page past the end answers with the last page
        // that has rows rather than an empty result the reader cannot get out of.
        var paged = paging.ClampTo(ranked.Count);

        var pageItems = ranked.Skip(paged.Skip).Take(paged.PageSize).ToList();

        // Groups are cut from the ranked page rather than from each module's own candidates, so a
        // result appears on the page it ranked into and on no other.
        var groups = new List<SearchGroupResponse>();

        foreach (var groupModule in ModuleOrder)
        {
            var items = pageItems
                .Where(candidate => candidate.Item.Module == groupModule)
                .Select(candidate => candidate.Item)
                .ToList();

            if (items.Count == 0)
            {
                continue;
            }

            groups.Add(new SearchGroupResponse(
                groupModule,
                ModuleLabels[groupModule],
                perModule[groupModule].Candidates.Count,
                items));
        }

        return new SearchResponse(
            preparedTerm,
            paged.Page,
            paged.PageSize,
            ranked.Count,
            truncated,
            groups);
    }

    // --------------------------------------------------------------------- the six queries

    // Each query projects into a private row record rather than straight into SearchCandidate,
    // because the mapping onto the response involves work SQL cannot translate: enum names, and a
    // district and province joined into one place string. The row records hold stored values only,
    // so every query stays translatable and the display work happens once, in memory.

    private sealed record StudentRow(
        Guid Id,
        string Slug,
        string Title,
        string Summary,
        ResourceType Type,
        string? Provider,
        DateOnly? ExpiresOn,
        DateTime CreatedAt);

    private sealed record JobRow(
        Guid Id,
        string Slug,
        string Title,
        string Organization,
        string Description,
        JobCategory Category,
        WorkMode WorkMode,
        DateOnly ApplicationDeadline,
        DateTime CreatedAt,
        string? DistrictName,
        string? ProvinceName);

    private sealed record BusinessRow(
        Guid Id,
        string Slug,
        string Name,
        string Description,
        BusinessCategory Category,
        string? Address,
        bool IsVerified,
        DateTime CreatedAt,
        string DistrictName,
        string ProvinceName);

    private sealed record DealRow(
        Guid Id,
        string Slug,
        string Title,
        string? Summary,
        string Description,
        string? DiscountText,
        DateOnly StartsOn,
        DateOnly EndsOn,
        DateTime CreatedAt,
        string? DistrictName,
        string? ProvinceName);

    private sealed record EventRow(
        Guid Id,
        string Slug,
        string Title,
        string? Summary,
        string Description,
        EventCategory Category,
        string? Organiser,
        string? Venue,
        DateTime StartsAt,
        DateTime EndsAt,
        DateTime CreatedAt,
        string DistrictName,
        string ProvinceName);

    private sealed record CalculatorRow(
        Guid Id,
        string Slug,
        string Name,
        string Summary,
        CalculatorCategory Category,
        DateTime CreatedAt);

    private async Task<ModuleCandidates> StudentsAsync(
        string pattern,
        string normalisedTerm,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // Newest first, so which rows fill the candidate cap is deterministic rather than whatever
        // order MySQL happened to return. No module can reach the cap at the current catalogue size.
        var rows = await PublicContentScope
            .StudentResources(dbContext.StudentResources, today)
            .Where(resource =>
                EF.Functions.Like(resource.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(resource.Summary, pattern, LikePattern.Escape))
            .OrderByDescending(resource => resource.CreatedAt)
            .ThenBy(resource => resource.Slug)
            .Take(SearchLimits.CandidateCap)
            .Select(resource => new StudentRow(
                resource.Id,
                resource.Slug,
                resource.Title,
                resource.Summary,
                resource.Type,
                resource.Provider,
                resource.ExpiresOn,
                resource.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ModuleCandidates(Rank(rows.Select(row => new SearchCandidate(
            SearchModule.Students,
            row.Title,
            row.Type.ToString(),
            row.Summary,
            string.Empty,
            row.CreatedAt,
            new SearchResultItem(
                SearchModule.Students,
                row.Id.ToString(),
                row.Slug,
                row.Title,
                row.Summary,
                row.Type.ToString(),
                null,
                null,
                row.Provider,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
row.ExpiresOn,
                    null,
                    null))), normalisedTerm, cancellationToken), rows.Count >= SearchLimits.CandidateCap);
    }

    private async Task<ModuleCandidates> JobsAsync(
        string pattern,
        string normalisedTerm,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var rows = await PublicContentScope
            .Jobs(dbContext.Jobs, today)
            .Where(job =>
                EF.Functions.Like(job.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(job.Organization, pattern, LikePattern.Escape)
                || EF.Functions.Like(job.Description, pattern, LikePattern.Escape))
            .OrderByDescending(job => job.CreatedAt)
            .ThenBy(job => job.Slug)
            .Take(SearchLimits.CandidateCap)
            .Select(job => new JobRow(
                job.Id,
                job.Slug,
                job.Title,
                job.Organization,
                job.Description,
                job.Category,
                job.WorkMode,
                job.ApplicationDeadline,
                job.CreatedAt,
                job.District == null ? null : job.District.Name,
                job.District == null ? null : job.District.Province.Name))
            .ToListAsync(cancellationToken);

        return new ModuleCandidates(Rank(rows.Select(row => new SearchCandidate(
            SearchModule.Jobs,
            row.Title,
            $"{row.Organization} {row.WorkMode} {row.Category}",
            string.Empty,
            row.Description,
            row.CreatedAt,
            new SearchResultItem(
                SearchModule.Jobs,
                row.Id.ToString(),
                row.Slug,
                row.Title,
                null,
                row.Category.ToString(),
                row.Organization,
                row.WorkMode.ToString(),
                null,
                null,
                null,
                null,
                Place(row.DistrictName, row.ProvinceName),
                false,
                null,
                null,
row.ApplicationDeadline,
                    null,
                    null,
                    null))), normalisedTerm, cancellationToken), rows.Count >= SearchLimits.CandidateCap);
    }

    private async Task<ModuleCandidates> BusinessesAsync(
        string pattern,
        string normalisedTerm,
        CancellationToken cancellationToken)
    {
        var rows = await PublicContentScope
            .Businesses(dbContext.Businesses)
            .Where(business =>
                EF.Functions.Like(business.Name, pattern, LikePattern.Escape)
                || EF.Functions.Like(business.Description, pattern, LikePattern.Escape)
                || EF.Functions.Like(business.Address ?? string.Empty, pattern, LikePattern.Escape))
            .OrderByDescending(business => business.CreatedAt)
            .ThenBy(business => business.Slug)
            .Take(SearchLimits.CandidateCap)
            .Select(business => new BusinessRow(
                business.Id,
                business.Slug,
                business.Name,
                business.Description,
                business.Category,
                business.Address,
                business.IsVerified,
                business.CreatedAt,
                business.District!.Name,
                business.District.Province.Name))
            .ToListAsync(cancellationToken);

        return new ModuleCandidates(Rank(rows.Select(row => new SearchCandidate(
            SearchModule.Businesses,
            row.Name,
            $"{row.Category} {row.Address}",
            string.Empty,
            row.Description,
            row.CreatedAt,
            new SearchResultItem(
                SearchModule.Businesses,
                row.Id.ToString(),
                row.Slug,
                row.Name,
                null,
                row.Category.ToString(),
                null,
                null,
                null,
                null,
                null,
                null,
                Place(row.DistrictName, row.ProvinceName),
row.IsVerified,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))), normalisedTerm, cancellationToken), rows.Count >= SearchLimits.CandidateCap);
    }

    private async Task<ModuleCandidates> DealsAsync(
        string pattern,
        string normalisedTerm,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var rows = await PublicContentScope
            .Deals(dbContext.Deals, today)
            .Where(deal =>
                EF.Functions.Like(deal.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(deal.Summary ?? string.Empty, pattern, LikePattern.Escape)
                || EF.Functions.Like(deal.Description, pattern, LikePattern.Escape)
                || EF.Functions.Like(deal.DiscountText ?? string.Empty, pattern, LikePattern.Escape))
            .OrderByDescending(deal => deal.CreatedAt)
            .ThenBy(deal => deal.Slug)
            .Take(SearchLimits.CandidateCap)
            .Select(deal => new DealRow(
                deal.Id,
                deal.Slug,
                deal.Title,
                deal.Summary,
                deal.Description,
                deal.DiscountText,
                deal.StartsOn,
                deal.EndsOn,
                deal.CreatedAt,
                deal.District == null ? null : deal.District.Name,
                deal.District == null ? null : deal.District.Province.Name))
            .ToListAsync(cancellationToken);

        return new ModuleCandidates(Rank(rows.Select(row => new SearchCandidate(
            SearchModule.Deals,
            row.Title,
            row.DiscountText ?? string.Empty,
            row.Summary ?? string.Empty,
            row.Description,
            row.CreatedAt,
            new SearchResultItem(
                SearchModule.Deals,
                row.Id.ToString(),
                row.Slug,
                row.Title,
                row.Summary,
                null,
                null,
                null,
                null,
                row.DiscountText,
                null,
                null,
                Place(row.DistrictName, row.ProvinceName),
                false,
row.StartsOn,
                    row.EndsOn,
                    null,
                    null,
                    null,
                    null))), normalisedTerm, cancellationToken), rows.Count >= SearchLimits.CandidateCap);
    }

    private async Task<ModuleCandidates> EventsAsync(
        string pattern,
        string normalisedTerm,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rows = await PublicContentScope
            .Events(dbContext.Events, now)
            .Where(calendarEvent =>
                EF.Functions.Like(calendarEvent.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Summary ?? string.Empty, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Description, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Organiser ?? string.Empty, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Venue ?? string.Empty, pattern, LikePattern.Escape))
            .OrderByDescending(calendarEvent => calendarEvent.CreatedAt)
            .ThenBy(calendarEvent => calendarEvent.Slug)
            .Take(SearchLimits.CandidateCap)
            .Select(calendarEvent => new EventRow(
                calendarEvent.Id,
                calendarEvent.Slug,
                calendarEvent.Title,
                calendarEvent.Summary,
                calendarEvent.Description,
                calendarEvent.Category,
                calendarEvent.Organiser,
                calendarEvent.Venue,
                calendarEvent.StartsAt,
                calendarEvent.EndsAt,
                calendarEvent.CreatedAt,
                calendarEvent.District!.Name,
                calendarEvent.District.Province.Name))
            .ToListAsync(cancellationToken);

        return new ModuleCandidates(Rank(rows.Select(row => new SearchCandidate(
            SearchModule.Events,
            row.Title,
            $"{row.Organiser} {row.Venue} {row.Category}",
            row.Summary ?? string.Empty,
            row.Description,
            row.CreatedAt,
            new SearchResultItem(
                SearchModule.Events,
                row.Id.ToString(),
                row.Slug,
                row.Title,
                row.Summary,
                row.Category.ToString(),
                null,
                null,
                null,
                null,
                row.Organiser,
                row.Venue,
                Place(row.DistrictName, row.ProvinceName),
                false,
                null,
                null,
null,
                    null,
                    row.StartsAt,
                    row.EndsAt))), normalisedTerm, cancellationToken), rows.Count >= SearchLimits.CandidateCap);
    }

    private async Task<ModuleCandidates> CalculatorsAsync(
        string pattern,
        string normalisedTerm,
        CancellationToken cancellationToken)
    {
        var rows = await PublicContentScope
            .Calculators(dbContext.Calculators)
            .Where(calculator =>
                EF.Functions.Like(calculator.Name, pattern, LikePattern.Escape)
                || EF.Functions.Like(calculator.Summary, pattern, LikePattern.Escape))
            .OrderByDescending(calculator => calculator.CreatedAt)
            .ThenBy(calculator => calculator.Slug)
            .Take(SearchLimits.CandidateCap)
            .Select(calculator => new CalculatorRow(
                calculator.Id,
                calculator.Slug,
                calculator.Name,
                calculator.Summary,
                calculator.Category,
                calculator.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ModuleCandidates(Rank(rows.Select(row => new SearchCandidate(
            SearchModule.Calculators,
            row.Name,
            row.Category.ToString(),
            row.Summary,
            string.Empty,
            row.CreatedAt,
            new SearchResultItem(
                SearchModule.Calculators,
                row.Id.ToString(),
                row.Slug,
                row.Name,
                row.Summary,
                row.Category.ToString(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))), normalisedTerm, cancellationToken), rows.Count >= SearchLimits.CandidateCap);
    }

    // ---------------------------------------------------------------------------- ranking

    /// <summary>
    /// Scores and drops candidates, keeping only those the ranker counts as a match.
    ///
    /// The re-check is not paranoia. The <c>LIKE</c> filter runs in MySQL under
    /// <c>utf8mb4_0900_ai_ci</c> while the ranking runs in .NET, and the two can disagree on inputs
    /// where normalisation matters. Keeping a candidate the ranker scored as a non-match would put an
    /// unexplained row on the page; dropping one it scored highly would hide a match the database
    /// found. This keeps the visible set equal to the ranked set.
    /// <para>
    /// Internal rather than private so the truncation contract can be tested directly: the flag is
    /// about the rows SQL returned, and a test has to be able to feed this a full cap of rows that
    /// mostly fail to rank in order to prove the two counts are not confused for one another.
    /// </para>
    /// </summary>
    internal static List<SearchCandidate> Rank(
        IEnumerable<SearchCandidate> candidates,
        string normalisedTerm,
        CancellationToken cancellationToken)
    {
        var ranked = new List<SearchCandidate>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scored = candidate with { Rank = RankOf(candidate, normalisedTerm) };

            if (scored.Rank >= 0)
            {
                ranked.Add(scored);
            }
        }

        return ranked;
    }

    /// <summary>
    /// Which tier a candidate matched, lowest first, or -1 for no match at all.
    ///
    /// A term in the title outranks the same term in the body, and a term that starts the title
    /// outranks one buried inside it. That is the whole ranking: five cheap string tests over a few
    /// hundred in-memory rows, rather than a scoring function nobody could explain to a reader whose
    /// result moved.
    /// </summary>
    private static int RankOf(SearchCandidate candidate, string normalisedTerm)
    {
        if (StartsWith(candidate.TitleHaystack, normalisedTerm))
        {
            return 0;
        }

        if (SearchText.Contains(candidate.TitleHaystack, normalisedTerm))
        {
            return 1;
        }

        if (SearchText.Contains(candidate.MetaHaystack, normalisedTerm))
        {
            return 2;
        }

        if (SearchText.Contains(candidate.SummaryHaystack, normalisedTerm))
        {
            return 3;
        }

        return SearchText.Contains(candidate.BodyHaystack, normalisedTerm) ? 4 : -1;
    }

    private static bool StartsWith(string title, string normalisedTerm) =>
        !string.IsNullOrEmpty(title)
        && SearchText.Normalise(title).StartsWith(normalisedTerm, StringComparison.Ordinal);

    /// <summary>
    /// "Kathmandu, Bagmati", or nothing for a row that applies everywhere.
    ///
    /// A nationwide offer and a remote job genuinely have no place, and leaving this null says so
    /// rather than showing an empty gap where the location normally sits.
    /// </summary>
    private static string? Place(string? districtName, string? provinceName)
    {
        if (string.IsNullOrWhiteSpace(districtName))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(provinceName) ? districtName : $"{districtName}, {provinceName}";
    }
}