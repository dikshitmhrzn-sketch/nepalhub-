using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Students;

namespace NepalHub.Api.Features.Deals;

public enum DealSaveStatus
{
    Created,
    Updated,
    NotFound,

    /// <summary>Shape or content rules were not met.</summary>
    Invalid,

    /// <summary>Another row already owns that slug.</summary>
    SlugTaken,

    /// <summary>The row is published, so its slug is the address it is live at and cannot move.</summary>
    SlugImmutable,
}

public enum DealDeleteStatus
{
    Deleted,
    NotFound,
    Published,
}

/// <summary>The validated, already-resolved form of an offers request.</summary>
public sealed record DealListQuery(
    PageRequest Paging,
    string? Search,
    Guid? ProvinceId,
    Guid? DistrictId,
    DealSort Sort);

public sealed record DealSaveOutcome(
    DealSaveStatus Status,
    DealAdminResponse? Deal = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

/// <summary>
/// The resolved ids behind a location filter. Both are nullable because each filter is optional, and a
/// district wins over its province when both are given, which mirrors how the query reads a narrower
/// filter over a broader one.
///
/// <para>
/// An offer with no district is a nationwide offer, and this is the one place the two modules differ in
/// a way that matters: a business listing always has a district, so a province filter is the broadest
/// question a reader asks there. Here a province filter answers only for offers that named a district,
/// and a nationwide offer appears under no province at all — which is correct, because it is not in
/// one. That is why the public cards show a blank location rather than "Nepal".
/// </para>
/// </summary>
public sealed record DealLocationFilter(Guid? ProvinceId, Guid? DistrictId, string? InvalidField = null);

/// <summary>
/// Every rule for the offers page: browsing it as a reader and managing it as staff. The controllers
/// only map outcomes onto HTTP, as the quiz, calculator, resource and job services do.
///
/// The rule that shapes this file is that published and unexpired are separate concerns that always
/// travel together on the public path. A draft is invisible because nobody has approved it; an offer
/// past its end date is invisible because it is worse than nothing to a reader who travels across town
/// on the strength of it. Both produce the same answer to a reader, and the same 404, so neither can be
/// probed.
///
/// <para>
/// The expiry comparison is made against the current Nepal date and the ranges are day-granular, so an
/// offer is live through the whole of its final day. This is the deliberate asymmetry with the event
/// calendar, which compares to the minute: nobody cares whether a lunch discount ended at six or at
/// midnight, but an event that starts at seven in the evening must not vanish during the afternoon it
/// is due to happen.
/// </para>
/// </summary>
public sealed class DealService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    ILogger<DealService> logger)
{
    /// <summary>
    /// A terms body shorter than this on a published row is almost always a placeholder nobody
    /// finished. An offers page of stubs is worse than a smaller one: a reader who decides not to
    /// travel because the conditions were never written has been let down by the page, and the business
    /// has not been.
    /// </summary>
    private const int MinPublishableDescription = 60;

    /// <summary>
    /// The longest offer this module will let staff publish. A year is not a real discount; anything
    /// longer is a price that has stopped being a discount, and it would sit on the page looking
    /// permanent.
    /// </summary>
    private const int MaxPublishableDays = 366;

    private const string RedemptionUrlField = "redemptionUrl";
    private const string DistrictField = "districtCode";
    private const string BusinessField = "businessSlug";

    // ---------------------------------------------------------------- public read

    /// <summary>
    /// The offers page. The published and unexpired predicates are applied here and cannot be switched
    /// off from the outside, which is deliberate: an <c>includeUnpublished</c> flag on a public endpoint
    /// is a way to leak drafts by accident, and a flag nobody can turn on by accident is not a flag.
    internal async Task<PagedResult<DealSummaryResponse>> ListAsync(
        DealListQuery request,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var query = ApplyFilters(
            PublicScope(today),
            request.Search,
            request.ProvinceId,
            request.DistrictId);

        var totalCount = await query.CountAsync(cancellationToken);

        // The district and its province are loaded for the page rather than projected in SQL, because
        // the card shows the province name. That is one extra join on at most one page of rows, and the
        // job board projects in SQL because its summary carries neither.
        var items = await ApplyPublicOrder(request.Sort, query)
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .Include(deal => deal.District)
                .ThenInclude(district => district!.Province)
            .Include(deal => deal.Business)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var summaries = items
            .Select(deal => ToSummaryResponse(deal, today))
            .ToList();

        return new PagedResult<DealSummaryResponse>(summaries, request.Paging.Page, request.Paging.PageSize, totalCount);
    }

    /// <summary>
    /// One offer as a reader sees it. Returns null for a draft, for an offer that has ended, and for a
    /// slug that never existed, identically, so this endpoint cannot be used to find out which slugs are
    /// real but not currently available.
    internal async Task<DealDetailResponse?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var today = dates.Today;

        var deal = await PublicScope(today)
            .Where(item => item.Slug == slug)
            .Include(item => item.District)
                .ThenInclude(district => district!.Province)
            .Include(item => item.Business)
            .FirstOrDefaultAsync(cancellationToken);

        return deal is null ? null : ToDetailResponse(deal, today);
    }

    /// <summary>
    /// Turns the province and district reference codes a caller sent into the ids the query needs.
    /// Codes rather than ids go over the wire because that is what the reference endpoints already
    /// publish, so the client never has to know a GUID, and a typo produces a specific
    /// <c>province_invalid</c> instead of silently matching nothing.
    internal async Task<DealLocationFilter> ResolveLocationFilterAsync(
        string? provinceCode,
        string? districtCode,
        CancellationToken cancellationToken)
    {
        Guid? provinceId = null;
        Guid? districtId = null;

        if (!string.IsNullOrWhiteSpace(provinceCode))
        {
            provinceId = await dbContext.Provinces
                .Where(province => province.Code == provinceCode.Trim())
                .Select(province => (Guid?)province.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (provinceId is null)
            {
                return new DealLocationFilter(null, null, "province");
            }
        }

        if (!string.IsNullOrWhiteSpace(districtCode))
        {
            districtId = await dbContext.Districts
                .Where(district => district.Code == districtCode.Trim())
                .Select(district => (Guid?)district.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (districtId is null)
            {
                return new DealLocationFilter(provinceId, null, "district");
            }
        }

        return new DealLocationFilter(provinceId, districtId);
    }

    // ---------------------------------------------------------------- staff read

    /// <summary>
    /// The staff offers list. Unlike the public list this shows drafts, and it also shows expired
    /// offers, because deciding what to retire is the whole point of the screen. The search, filters
    /// and paging are the same shape the reader uses, so there is one convention to learn.
    internal async Task<PagedResult<DealAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        DealAdminFilter filter,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var query = ApplyAdminFilters(
            dbContext.Deals.AsNoTracking(),
            search,
            provinceId,
            districtId,
            filter);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(deal => deal.UpdatedAt)
            .ThenBy(deal => deal.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(deal => new DealAdminSummaryResponse(
                deal.Id,
                deal.Slug,
                deal.Title,
                deal.District == null ? null : deal.District.Name,
                deal.Business == null ? null : deal.Business.Name,
                deal.IsPublished,
                // Expired is computed against the current Nepal date rather than stored, for the same
                // reason "open now" is on a business: an offer does not get a flag when its end date
                // passes, it simply stops being live.
                deal.EndsOn < today,
                deal.EndsOn,
                deal.UpdatedAt,
                deal.CreatedByUser == null ? null : deal.CreatedByUser.Email))
            .ToListAsync(cancellationToken);

        return new PagedResult<DealAdminSummaryResponse>(items, paging.Page, paging.PageSize, totalCount);
    }

    internal async Task<DealAdminResponse?> FindForAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        // The business is loaded because the editor has to be handed back the slug it was linked by.
        // Leaving it out would answer with a null businessSlug on a linked offer, and saving that row
        // back would quietly drop the link.
        var deal = await dbContext.Deals
            .AsNoTracking()
            .Include(item => item.District)
                .ThenInclude(district => district!.Province)
            .Include(item => item.Business)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (deal is null)
        {
            return null;
        }

        var author = await ResolveAuthorEmailAsync(deal.CreatedByUserId, cancellationToken);

        return ToAdminResponse(deal, author);
    }

    /// <summary>
    /// The published businesses a staff member can attach an offer to. Used to populate the editor's
    /// picker, which is why it returns names rather than ids: the choice a staff member makes is "this
    /// business", and asking them to match a GUID against a name they know would be pointless.
    /// </summary>
    internal async Task<IReadOnlyList<DealBusinessOptionResponse>> ListBusinessOptionsAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Businesses
            .AsNoTracking()
            .Where(business => business.IsPublished);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());

            query = query.Where(business =>
                EF.Functions.Like(business.Name, pattern, LikePattern.Escape)
                || EF.Functions.Like(business.Slug, pattern, LikePattern.Escape));
        }

        var options = await query
            .OrderBy(business => business.Name)
            .ThenBy(business => business.Id)
            .Take(100)
            .Select(business => new DealBusinessOptionResponse(business.Slug, business.Name))
            .ToListAsync(cancellationToken);

        return options;
    }

    // ---------------------------------------------------------------- staff writes

    internal async Task<DealSaveOutcome> CreateAsync(
        SaveDealRequest request,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, isGoingLive: request.IsPublished, cancellationToken);
        if (errors.Count > 0)
        {
            return new DealSaveOutcome(DealSaveStatus.Invalid, FieldErrors: errors);
        }

        if (await SlugExistsAsync(request.Slug, cancellationToken))
        {
            return new DealSaveOutcome(
                DealSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug.Trim()}' is already used by another offer.");
        }

        var districtId = await ResolveOptionalDistrictAsync(request.DistrictCode, cancellationToken);
        var businessId = await ResolveOptionalBusinessAsync(request.BusinessSlug, cancellationToken);

        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
            StartsOn = request.StartsOn!.Value,
            EndsOn = request.EndsOn!.Value,
        };

        ApplyCommonFields(deal, request, districtId, businessId);

        dbContext.Deals.Add(deal);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index is the real guard. Two staff members saving the same slug at once is an
            // expected outcome, not a fault, and losing it is reported as the conflict it is rather
            // than as a server error.
            logger.LogInformation(exception, "A duplicate deal slug was refused for {Slug}.", request.Slug);
            dbContext.Entry(deal).State = EntityState.Detached;

            return new DealSaveOutcome(
                DealSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug.Trim()}' is already used by another offer.");
        }

        return new DealSaveOutcome(DealSaveStatus.Created, await FindForAdminAsync(deal.Id, cancellationToken));
    }

    internal async Task<DealSaveOutcome> UpdateAsync(
        Guid id,
        SaveDealRequest request,
        CancellationToken cancellationToken)
    {
        var deal = await dbContext.Deals
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (deal is null)
        {
            return new DealSaveOutcome(DealSaveStatus.NotFound);
        }

        // A published slug is the address the offer is live at, so it is frozen from that moment on. A
        // draft keeps its editable slug, because a staff member who mistyped it has not yet sent anyone
        // to the wrong URL and can still fix it.
        if (deal.IsPublished
            && !string.Equals(deal.Slug, request.Slug.Trim(), StringComparison.Ordinal))
        {
            return new DealSaveOutcome(
                DealSaveStatus.SlugImmutable,
                Message: "The slug cannot be changed once an offer is published, because that is the address people are already sharing. Unpublish it first if the address really has to change.");
        }

        // A row that is already public may be corrected in place, including being pulled in early, so
        // the publish rules apply only to the transition from draft to live. Applying them to every
        // save of a published row would make ending an offer in advance impossible through the editor,
        // and that is exactly what a live offer with wrong dates needs.
        var errors = await ValidateAsync(
            request,
            isGoingLive: request.IsPublished && !deal.IsPublished,
            cancellationToken);
        if (errors.Count > 0)
        {
            return new DealSaveOutcome(DealSaveStatus.Invalid, FieldErrors: errors);
        }

        var trimmedSlug = request.Slug.Trim();
        if (!string.Equals(deal.Slug, trimmedSlug, StringComparison.Ordinal)
            && await SlugExistsAsync(trimmedSlug, cancellationToken))
        {
            return new DealSaveOutcome(
                DealSaveStatus.SlugTaken,
                Message: $"The slug '{trimmedSlug}' is already used by another offer.");
        }

        var districtId = await ResolveOptionalDistrictAsync(request.DistrictCode, cancellationToken);
        var businessId = await ResolveOptionalBusinessAsync(request.BusinessSlug, cancellationToken);

        ApplyCommonFields(deal, request, districtId, businessId);
        deal.StartsOn = request.StartsOn!.Value;
        deal.EndsOn = request.EndsOn!.Value;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The same race the create path guards: the pre-check above narrows the window but does not
            // close it, and losing it is a conflict rather than a server error.
            logger.LogInformation(exception, "A duplicate deal slug was refused for {Slug}.", trimmedSlug);
            dbContext.ChangeTracker.Clear();

            return new DealSaveOutcome(
                DealSaveStatus.SlugTaken,
                Message: $"The slug '{trimmedSlug}' is already used by another offer.");
        }

        return new DealSaveOutcome(DealSaveStatus.Updated, await FindForAdminAsync(id, cancellationToken));
    }

    /// <summary>
    /// Removes a draft. A published row is refused, because the intended operation is to retract it,
    /// and deleting it instead loses the record of what readers were once shown.
    internal async Task<DealDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deal = await dbContext.Deals
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (deal is null)
        {
            return DealDeleteStatus.NotFound;
        }

        if (deal.IsPublished)
        {
            return DealDeleteStatus.Published;
        }

        dbContext.Deals.Remove(deal);
        await dbContext.SaveChangesAsync(cancellationToken);

        return DealDeleteStatus.Deleted;
    }

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// Published and not ended, on the Nepal calendar. The end comparison is inclusive because an offer
    /// is live through its last day: someone reading at six in the evening on the final date is
    /// exactly the reader this page exists for.
    /// </summary>
    private IQueryable<Deal> PublicScope(DateOnly today) =>
        dbContext.Deals
            .AsNoTracking()
            .Where(deal => deal.IsPublished && deal.EndsOn >= today);

    private static IQueryable<Deal> ApplyFilters(
        IQueryable<Deal> query,
        string? search,
        Guid? provinceId,
        Guid? districtId)
    {
        if (districtId is { } selectedDistrict)
        {
            query = query.Where(deal => deal.DistrictId == selectedDistrict);
        }
        else if (provinceId is { } selectedProvince)
        {
            // A province filter is a filter on the district's province, not a column on the row. There
            // is no province_id on deals precisely so the two cannot disagree, and this is what that
            // decision costs: one join in the filter.
            //
            // Offers with no district are excluded here, which is deliberate and not an oversight. A
            // nationwide offer is not in a province, so listing it under all seven would be a claim
            // about where it applies that nobody made.
            query = query.Where(deal => deal.District != null && deal.District.ProvinceId == selectedProvince);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());

            // Title, summary, terms and the offer text itself. The redemption link is left out
            // deliberately: searching over URLs would let a visitor probe which businesses staff have
            // attached links to, which is not what an offers page is for.
            query = query.Where(deal =>
                EF.Functions.Like(deal.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(deal.Summary ?? string.Empty, pattern, LikePattern.Escape)
                || EF.Functions.Like(deal.Description, pattern, LikePattern.Escape)
                || EF.Functions.Like(deal.DiscountText ?? string.Empty, pattern, LikePattern.Escape));
        }

        return query;
    }

    private static IQueryable<Deal> ApplyAdminFilters(
        IQueryable<Deal> query,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        DealAdminFilter filter)
    {
        query = ApplyFilters(query, search, provinceId, districtId);

        return filter switch
        {
            DealAdminFilter.Published => query.Where(deal => deal.IsPublished),
            DealAdminFilter.Draft => query.Where(deal => !deal.IsPublished),
            _ => query,
        };
    }

    /// <summary>
    /// The sort is chosen from a fixed set and applied as a real <c>OrderBy</c>, never concatenated
    /// into a string. The identifier ends every ordering so that two offers ending the same day keep a
    /// stable order between pages, which is what stops one appearing on both page 1 and page 2.
    private static IQueryable<Deal> ApplyPublicOrder(DealSort sort, IQueryable<Deal> query) => sort switch
    {
        DealSort.Newest => query
            .OrderByDescending(deal => deal.CreatedAt)
            .ThenBy(deal => deal.Id),

        _ => query
            .OrderBy(deal => deal.EndsOn)
            .ThenBy(deal => deal.Id),
    };

    private static void ApplyCommonFields(
        Deal deal,
        SaveDealRequest request,
        Guid? districtId,
        Guid? businessId)
    {
        deal.Slug = request.Slug.Trim();
        deal.Title = request.Title.Trim();
        deal.Summary = Blank(request.Summary);
        deal.Description = request.Description.Trim();
        deal.DiscountText = Blank(request.DiscountText);
        deal.BusinessId = businessId;
        deal.DistrictId = districtId;
        deal.RedemptionUrl = NormaliseRedemptionUrl(request.RedemptionUrl);
        deal.IsPublished = request.IsPublished;
    }

    private async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        await dbContext.Deals
            .AnyAsync(deal => deal.Slug == slug.Trim(), cancellationToken);

    /// <summary>
    /// The business an offer belongs to, or null. Resolved by slug and deliberately not checked for
    /// publication: drafting an offer against a draft business is the normal order of work, and a slug
    /// that matches nothing is stored unlinked rather than refused, so the offer is not blocked by a
    /// business listing that has not been created yet.
    /// </summary>
    private async Task<Guid?> ResolveOptionalBusinessAsync(string? businessSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(businessSlug))
        {
            return null;
        }

        return await dbContext.Businesses
            .Where(business => business.Slug == businessSlug.Trim())
            .Select(business => (Guid?)business.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveOptionalDistrictAsync(string? districtCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(districtCode))
        {
            return null;
        }

        return await dbContext.Districts
            .Where(district => district.Code == districtCode.Trim())
            .Select(district => (Guid?)district.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Everything that must be true before an offer is worth showing a reader who may travel on the
    /// strength of it, as field errors against the inputs the staff form actually has.
    ///
    /// This is the single source of the publish rules. The save path returns these as the reason a save
    /// was refused, and the staff editor returns the same list as <c>publishBlockers</c>, so what the
    /// editor tells you and what the server enforces cannot drift apart.
    internal IReadOnlyList<FieldError> CollectPublishBlockers(SaveDealRequest request)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.DiscountText))
        {
            errors.Add(new FieldError(
                "discountText",
                "required",
                "Say what the offer is before publishing. An offer with no offer in it is a heading, and a reader who crossed town for it has been let down."));
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            errors.Add(new FieldError(
                "summary",
                "required",
                "Write a one-line summary. It is what readers scan on the offers page, and without it the listing is a title with nothing under it."));
        }

        if (request.Description.Trim().Length < MinPublishableDescription)
        {
            errors.Add(new FieldError(
                "description",
                "required",
                $"Write the terms before publishing, at least {MinPublishableDescription} characters. What is and is not included decides whether the reader is annoyed or only disappointed."));
        }

        // A published offer must still be running. Refusing here means an offer that has already ended
        // cannot be put back in front of readers without the dates being corrected first, which is the
        // only order that makes sense: nobody wants yesterday's discount back.
        if (request.StartsOn is { } startsOn && request.EndsOn is { } endsOn)
        {
            var today = CurrentNepalDate();

            if (endsOn < today)
            {
                errors.Add(new FieldError(
                    "endsOn",
                    "out_of_range",
                    "This offer has already ended, so it cannot be published. Move the end date forward, or save it as a draft."));
            }
            else
            {
                var days = endsOn.DayNumber - startsOn.DayNumber;

                if (days > MaxPublishableDays)
                {
                    errors.Add(new FieldError(
                        "endsOn",
                        "out_of_range",
                        $"That offer would run for {days} days. A year is as long as a discount stays a discount; anything longer is a price, and belongs on the business listing instead."));
                }
            }
        }

        return errors;
    }

    private async Task<IReadOnlyList<FieldError>> ValidateAsync(
        SaveDealRequest request,
        bool isGoingLive,
        CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();

        if (!string.IsNullOrWhiteSpace(request.DistrictCode)
            && await ResolveOptionalDistrictAsync(request.DistrictCode, cancellationToken) is null)
        {
            errors.Add(new FieldError(
                DistrictField,
                "invalid",
                $"'{request.DistrictCode.Trim()}' is not a district this site knows about. Choose one from the list, or leave it empty if the offer applies nationwide."));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessSlug)
            && !await BusinessExistsAsync(request.BusinessSlug, cancellationToken))
        {
            // Unknown rather than invalid, because the staff member did nothing wrong: the business may
            // simply not be listed yet. This is a warning about the link, not a reason to refuse the
            // save, so it is not in the error list and the offer is stored unlinked.
            logger.LogInformation(
                "Deal {Slug} names business '{BusinessSlug}', which is not listed.",
                request.Slug,
                request.BusinessSlug.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.RedemptionUrl)
            && !ExternalUrlPolicy.TryNormalise(request.RedemptionUrl, RedemptionUrlField, out _, out var urlError))
        {
            errors.Add(new FieldError(RedemptionUrlField, "invalid", urlError!));
        }

        if (request.StartsOn is { } startsOn && request.EndsOn is { } endsOn && endsOn < startsOn)
        {
            errors.Add(new FieldError(
                "endsOn",
                "out_of_range",
                "The offer ends before it starts. Move the end date later, or the start date earlier."));
        }

        // Only when the offer is actually going out. A live row being corrected may well need its end
        // date moved in, and refusing that edit would make it impossible to end an offer early through
        // the screen that exists to do it.
        if (isGoingLive)
        {
            errors.AddRange(CollectPublishBlockers(request));
        }

        return errors;
    }

    private async Task<bool> BusinessExistsAsync(string slug, CancellationToken cancellationToken) =>
        await dbContext.Businesses
            .AnyAsync(business => business.Slug == slug.Trim(), cancellationToken);

    private async Task<string?> ResolveAuthorEmailAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is null)
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? NormaliseRedemptionUrl(string? raw) =>
        ExternalUrlPolicy.TryNormalise(raw, RedemptionUrlField, out var normalised, out _) ? normalised : null;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DealSummaryResponse ToSummaryResponse(Deal deal, DateOnly today) => new(
        deal.Id,
        deal.Slug,
        deal.Title,
        deal.Summary,
        deal.DiscountText,
        deal.Business?.Name,
        deal.Business?.Slug,
        deal.District?.Name ?? string.Empty,
        deal.District?.Province.Name ?? string.Empty,
        deal.StartsOn,
        deal.EndsOn,
        DaysRemaining(deal.EndsOn, today),
        deal.CreatedAt);

    private static DealDetailResponse ToDetailResponse(Deal deal, DateOnly today) => new(
        deal.Id,
        deal.Slug,
        deal.Title,
        deal.Summary,
        deal.Description,
        deal.DiscountText,
        deal.Business?.Name,
        PublicBusinessSlug(deal.Business),
        deal.District?.Name,
        deal.District?.Province.Name,
        deal.RedemptionUrl,
        deal.StartsOn,
        deal.EndsOn,
        DaysRemaining(deal.EndsOn, today),
        IsRunning(deal.StartsOn, deal.EndsOn, today),
        deal.UpdatedAt);

    /// <summary>
    /// The business slug a reader may be given, or null when there is nothing to link to.
    /// <para>
    /// The name is always reported, because an offer whose business has been retracted still has to
    /// say whose offer it was. The slug is a link, and a link to a business that is not listed leads
    /// nowhere: the business page answers 404 for a draft exactly as it does for a slug that never
    /// existed. Handing out the slug of an unlisted business would also leak the fact that some
    /// business by that name exists at all, which is the one thing a draft is supposed to hide.
    /// </para>
    /// </summary>
    private static string? PublicBusinessSlug(Business? business) =>
        business is { IsPublished: true } ? business.Slug : null;

    private DealAdminResponse ToAdminResponse(Deal deal, string? author)
    {
        var district = deal.District;
        var province = district?.Province;
        var today = dates.Today;

        return new DealAdminResponse(
            deal.Id,
            deal.Slug,
            deal.Title,
            deal.Summary,
            deal.Description,
            deal.DiscountText,
            deal.Business?.Slug,
            district?.Code,
            district?.Name,
            province?.Code,
            province?.Name,
            deal.RedemptionUrl,
            deal.StartsOn,
            deal.EndsOn,
            deal.IsPublished,
            deal.EndsOn < today,
            deal.CreatedAt,
            deal.UpdatedAt,
            author,
            deal.IsPublished
                ? []
                : CollectPublishBlockers(ToSaveRequest(deal)).Select(error => error.Message).ToList());
    }

    /// <summary>
    /// Re-projects a stored row back into the request shape so the publish rules can be evaluated
    /// against it. Without this, the editor's blocker list would need a second copy of every rule, and
    /// the two copies would eventually disagree.
    private static SaveDealRequest ToSaveRequest(Deal deal) => new()
    {
        Slug = deal.Slug,
        Title = deal.Title,
        Summary = deal.Summary,
        Description = deal.Description,
        DiscountText = deal.DiscountText,
        BusinessSlug = deal.Business?.Slug,
        DistrictCode = deal.District?.Code,
        RedemptionUrl = deal.RedemptionUrl,
        StartsOn = deal.StartsOn,
        EndsOn = deal.EndsOn,
        IsPublished = deal.IsPublished,
    };

    /// <summary>
    /// Whole days until the offer ends, counted inclusively so an offer ending today reads as one day
    /// left rather than none. Never negative: the public scope has already excluded anything past its
    /// end date, and the staff list shows expired rows with the flag rather than a count that would
    /// only be meaningful for one of the two callers.
    /// </summary>
    private static int DaysRemaining(DateOnly endsOn, DateOnly today) =>
        Math.Max(0, endsOn.DayNumber - today.DayNumber + 1);

    private static bool IsRunning(DateOnly startsOn, DateOnly endsOn, DateOnly today) =>
        today >= startsOn && today <= endsOn;

    /// <summary>
    /// The Nepal date the publish rules compare against. Read from the injected provider rather than
    /// the clock so a test can move the date without touching the machine's.
    /// </summary>
    private DateOnly CurrentNepalDate() => dates.Today;

    /// <summary>
    /// MySQL error 1062 is ER_DUP_ENTRY, matching the job and business write paths. Catching every
    /// <see cref="DbUpdateException"/> instead would report a dead connection or a truncated column as
    /// a friendly "that slug is taken".
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}