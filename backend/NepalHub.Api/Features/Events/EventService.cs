using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Students;

namespace NepalHub.Api.Features.Events;

public enum EventSaveStatus
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

public enum EventDeleteStatus
{
    Deleted,
    NotFound,
    Published,
}

/// <summary>The validated, already-resolved form of a calendar request.</summary>
public sealed record EventListQuery(
    PageRequest Paging,
    string? Search,
    Guid? ProvinceId,
    Guid? DistrictId,
    EventCategory? Category,
    EventSort Sort);

public sealed record EventSaveOutcome(
    EventSaveStatus Status,
    EventAdminResponse? Event = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

/// <summary>
/// The resolved ids behind a location filter. Both are nullable because each filter is optional, and a
/// district wins over its province when both are given, which mirrors how the query reads a narrower
/// filter over a broader one.
///
/// <para>
/// <see cref="InvalidField"/> names the first argument whose code was not recognised, so the controller
/// can answer with a field-specific error rather than a generic "no results". Null means everything
/// given was understood.
/// </para>
/// </summary>
public sealed record EventLocationFilter(Guid? ProvinceId, Guid? DistrictId, string? InvalidField = null);

/// <summary>
/// Every rule for the events calendar: browsing it as a reader and managing it as staff. The controllers
/// only map outcomes onto HTTP, as the quiz, calculator, resource and job services do.
///
/// The rule that shapes most of this file is that published and unfinished are separate concerns that
/// always travel together on the public path. A draft is invisible because nobody has approved it; a
/// finished event is invisible because a reader who travels to a festival that ended last week has been
/// sent to their time's cost. Both produce the same answer to a reader, and the same 404, so neither can
/// be probed.
///
/// <para>
/// The expiry comparison is made against the current Nepal time and to the minute, which is the
/// deliberate asymmetry with the offers page. An event that starts at seven in the evening must still
/// be on the calendar at four in the afternoon, and a day-granular comparison would have dropped it.
/// An offer does not need the same precision, which is why offers store dates and this stores times.
/// </para>
/// </summary>
public sealed class EventService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    ILogger<EventService> logger)
{
    /// <summary>
    /// A description shorter than this on a published row is almost always a placeholder nobody
    /// finished. A calendar of stubs is worse than a smaller one: a reader who plans a trip around a
    /// three-line listing has committed real effort to something nobody described.
    /// </summary>
    private const int MinPublishableDescription = 60;

    /// <summary>
    /// The longest single event this module will let staff publish, in hours. A week is not an event,
    /// it is a shift pattern or a residency, and a row spanning it would sit on the calendar looking
    /// permanently on.
    /// </summary>
    private const int MaxPublishableHours = 168;

    private const string RegistrationUrlField = "registrationUrl";
    private const string DistrictField = "districtCode";

    // ---------------------------------------------------------------- public read

    /// <summary>
    /// The calendar. The published and unfinished predicates are applied here and cannot be switched off
    /// from the outside, which is deliberate: an <c>includeUnpublished</c> flag on a public endpoint is a
    /// way to leak drafts by accident, and a flag nobody can turn on by accident is not a flag.
    ///
    /// An event that is under way stays on the list until it ends, so a reader opening this page during
    /// an event can still find it. That is what the <c>EndsAt &gt;= now</c> half of the predicate is
    /// for: it is why the start time does not have to be in the future.
    internal async Task<PagedResult<EventSummaryResponse>> ListAsync(
        EventListQuery request,
        CancellationToken cancellationToken)
    {
        var now = dates.NepalNow;
        var query = ApplyFilters(
            PublicScope(now),
            request.Search,
            request.ProvinceId,
            request.DistrictId,
            request.Category);

        var totalCount = await query.CountAsync(cancellationToken);

        // The district, its province and the host business are loaded for the page rather than
        // projected in SQL, because the card shows the province name and the business name. That is two
        // extra joins on at most one page of rows, which is the trade this module accepts.
        var items = await ApplyPublicOrder(request.Sort, query)
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .Include(calendarEvent => calendarEvent.District)
                .ThenInclude(district => district!.Province)
            .Include(calendarEvent => calendarEvent.Business)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var summaries = items
            .Select(calendarEvent => ToSummaryResponse(calendarEvent, now))
            .ToList();

        return new PagedResult<EventSummaryResponse>(summaries, request.Paging.Page, request.Paging.PageSize, totalCount);
    }

    /// <summary>
    /// One event as a reader sees it. Returns null for a draft, for an event that has finished, and for
    /// a slug that never existed, identically, so this endpoint cannot be used to find out which slugs
    /// are real but not currently on the calendar.
    internal async Task<EventDetailResponse?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var now = dates.NepalNow;

        var calendarEvent = await PublicScope(now)
            .Where(item => item.Slug == slug)
            .Include(item => item.District)
                .ThenInclude(district => district!.Province)
            .Include(item => item.Business)
            .FirstOrDefaultAsync(cancellationToken);

        return calendarEvent is null ? null : ToDetailResponse(calendarEvent, now);
    }

    /// <summary>
    /// Turns the province and district reference codes a caller sent into the ids the query needs. Codes
    /// rather than ids go over the wire because that is what the reference endpoints already publish, so
    /// the client never has to know a GUID, and a typo produces a specific <c>province_invalid</c>
    /// instead of silently matching nothing.
    internal async Task<EventLocationFilter> ResolveLocationFilterAsync(
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
                return new EventLocationFilter(null, null, "province");
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
                return new EventLocationFilter(provinceId, null, "district");
            }
        }

        return new EventLocationFilter(provinceId, districtId);
    }

    // ---------------------------------------------------------------- staff read

    /// <summary>
    /// The staff calendar. Unlike the public list this shows drafts, and it also shows finished events,
    /// because deciding what to retire is the whole point of the screen. The search, filters and paging
    /// are the same shape the reader uses, so there is one convention to learn.
    internal async Task<PagedResult<EventAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        EventCategory? category,
        EventAdminFilter filter,
        CancellationToken cancellationToken)
    {
        var now = dates.NepalNow;
        var query = ApplyAdminFilters(
            dbContext.Events.AsNoTracking(),
            search,
            provinceId,
            districtId,
            category,
            filter);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(calendarEvent => calendarEvent.UpdatedAt)
            .ThenBy(calendarEvent => calendarEvent.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(calendarEvent => new EventAdminSummaryResponse(
                calendarEvent.Id,
                calendarEvent.Slug,
                calendarEvent.Title,
                calendarEvent.Category,
                calendarEvent.District == null ? string.Empty : calendarEvent.District.Name,
                calendarEvent.IsPublished,
                // Finished is computed against the current Nepal time rather than stored, for the same
                // reason "open now" is on a business: an event does not get a flag when it ends, it
                // simply stops being on the calendar.
                calendarEvent.EndsAt < now,
                calendarEvent.StartsAt,
                calendarEvent.UpdatedAt,
                calendarEvent.CreatedByUser == null ? null : calendarEvent.CreatedByUser.Email))
            .ToListAsync(cancellationToken);

        return new PagedResult<EventAdminSummaryResponse>(items, paging.Page, paging.PageSize, totalCount);
    }

    internal async Task<EventAdminResponse?> FindForAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        // The business is loaded because the editor has to be handed back the slug it was linked by.
        // Leaving it out would answer with a null businessSlug on a linked event, and saving that row
        // back would quietly drop the link.
        var calendarEvent = await dbContext.Events
            .AsNoTracking()
            .Include(item => item.District)
                .ThenInclude(district => district!.Province)
            .Include(item => item.Business)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (calendarEvent is null)
        {
            return null;
        }

        var author = await ResolveAuthorEmailAsync(calendarEvent.CreatedByUserId, cancellationToken);

        return ToAdminResponse(calendarEvent, author);
    }

    /// <summary>
    /// The published businesses an event can be attached to, for the staff editor's picker. Separate from
    /// the events list because it is a different question with a different answer shape, and it must not
    /// be able to return an unpublished listing: there is nothing to attach to.
    internal async Task<IReadOnlyList<EventBusinessOptionResponse>> ListBusinessOptionsAsync(
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
            .Select(business => new EventBusinessOptionResponse(business.Slug, business.Name))
            .ToListAsync(cancellationToken);

        return options;
    }

    // ---------------------------------------------------------------- staff writes

    internal async Task<EventSaveOutcome> CreateAsync(
        SaveEventRequest request,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, isGoingLive: request.IsPublished, cancellationToken);
        if (errors.Count > 0)
        {
            return new EventSaveOutcome(EventSaveStatus.Invalid, FieldErrors: errors);
        }

        if (await SlugExistsAsync(request.Slug, cancellationToken))
        {
            return new EventSaveOutcome(
                EventSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug.Trim()}' is already used by another event.");
        }

        var districtId = await ResolveRequiredDistrictAsync(request.DistrictCode, cancellationToken);
        var businessId = await ResolveOptionalBusinessAsync(request.BusinessSlug, cancellationToken);

        var calendarEvent = new Event
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
            StartsAt = request.StartsAt!.Value,
            EndsAt = request.EndsAt!.Value,
        };

        ApplyCommonFields(calendarEvent, request, districtId, businessId);

        dbContext.Events.Add(calendarEvent);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index is the real guard. Two staff members saving the same slug at once is an
            // expected outcome, not a fault, and losing it is reported as the conflict it is rather than
            // as a server error.
            logger.LogInformation(exception, "A duplicate event slug was refused for {Slug}.", request.Slug);
            dbContext.Entry(calendarEvent).State = EntityState.Detached;

            return new EventSaveOutcome(
                EventSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug.Trim()}' is already used by another event.");
        }

        return new EventSaveOutcome(EventSaveStatus.Created, await FindForAdminAsync(calendarEvent.Id, cancellationToken));
    }

    internal async Task<EventSaveOutcome> UpdateAsync(
        Guid id,
        SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await dbContext.Events
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (calendarEvent is null)
        {
            return new EventSaveOutcome(EventSaveStatus.NotFound);
        }

        // A published slug is the address the event is live at, so it is frozen from that moment on. A
        // draft keeps its editable slug, because a staff member who mistyped it has not yet sent anyone
        // to the wrong URL and can still fix it.
        if (calendarEvent.IsPublished
            && !string.Equals(calendarEvent.Slug, request.Slug.Trim(), StringComparison.Ordinal))
        {
            return new EventSaveOutcome(
                EventSaveStatus.SlugImmutable,
                Message: "The slug cannot be changed once an event is published, because that is the address people are already sharing. Unpublish it first if the address really has to change.");
        }

        // A row that is already public may be corrected in place, including being brought forward or
        // cancelled, so the publish rules apply only to the transition from draft to live. Applying them
        // to every save of a published row would make pulling a published event forward impossible
        // through the editor, which is exactly what a live event with wrong dates needs.
        var errors = await ValidateAsync(
            request,
            isGoingLive: request.IsPublished && !calendarEvent.IsPublished,
            cancellationToken);
        if (errors.Count > 0)
        {
            return new EventSaveOutcome(EventSaveStatus.Invalid, FieldErrors: errors);
        }

        var trimmedSlug = request.Slug.Trim();
        if (!string.Equals(calendarEvent.Slug, trimmedSlug, StringComparison.Ordinal)
            && await SlugExistsAsync(trimmedSlug, cancellationToken))
        {
            return new EventSaveOutcome(
                EventSaveStatus.SlugTaken,
                Message: $"The slug '{trimmedSlug}' is already used by another event.");
        }

        var districtId = await ResolveRequiredDistrictAsync(request.DistrictCode, cancellationToken);
        var businessId = await ResolveOptionalBusinessAsync(request.BusinessSlug, cancellationToken);

        ApplyCommonFields(calendarEvent, request, districtId, businessId);
        calendarEvent.StartsAt = request.StartsAt!.Value;
        calendarEvent.EndsAt = request.EndsAt!.Value;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The same race the create path guards: the pre-check above narrows the window but does not
            // close it, and losing it is a conflict rather than a server error.
            logger.LogInformation(exception, "A duplicate event slug was refused for {Slug}.", trimmedSlug);
            dbContext.ChangeTracker.Clear();

            return new EventSaveOutcome(
                EventSaveStatus.SlugTaken,
                Message: $"The slug '{trimmedSlug}' is already used by another event.");
        }

        return new EventSaveOutcome(EventSaveStatus.Updated, await FindForAdminAsync(id, cancellationToken));
    }

    /// <summary>
    /// Removes a draft. A published row is refused, because the intended operation is to retract it, and
    /// deleting it instead loses the record of what readers were once shown.
    internal async Task<EventDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var calendarEvent = await dbContext.Events
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (calendarEvent is null)
        {
            return EventDeleteStatus.NotFound;
        }

        if (calendarEvent.IsPublished)
        {
            return EventDeleteStatus.Published;
        }

        dbContext.Events.Remove(calendarEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return EventDeleteStatus.Deleted;
    }

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// Published and not finished, to the minute, on the Nepal clock. The end comparison is inclusive so
    /// an event is still listed at the moment it ends rather than vanishing mid-sentence in the middle
    /// of a session.
    ///
    /// There is deliberately no start comparison. An event that begins at seven this evening is still
    /// the thing a reader needs to see at four this afternoon, and filtering on the start would put it
    /// past the reader's reach for most of the day it matters.
    /// </summary>
    private IQueryable<Event> PublicScope(DateTime now) =>
        dbContext.Events
            .AsNoTracking()
            .Where(calendarEvent => calendarEvent.IsPublished && calendarEvent.EndsAt >= now);

    private static IQueryable<Event> ApplyFilters(
        IQueryable<Event> query,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        EventCategory? category)
    {
        if (districtId is { } selectedDistrict)
        {
            query = query.Where(calendarEvent => calendarEvent.DistrictId == selectedDistrict);
        }
        else if (provinceId is { } selectedProvince)
        {
            // A province filter is a filter on the district's province, not a column on the row. There is
            // no province_id on events precisely so the two cannot disagree, and this is what that
            // decision costs: one join in the filter.
            query = query.Where(calendarEvent => calendarEvent.District != null && calendarEvent.District.ProvinceId == selectedProvince);
        }

        if (category is { } selectedCategory)
        {
            query = query.Where(calendarEvent => calendarEvent.Category == selectedCategory);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());

            // Title, summary, terms, organiser and venue. The venue is searchable because a reader who
            // knows "the Pokhara one" should find it, and the registration link is left out for the same
            // reason it is on an offer: searching over URLs would let a visitor probe which links staff
            // have attached.
            query = query.Where(calendarEvent =>
                EF.Functions.Like(calendarEvent.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Summary ?? string.Empty, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Description, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Organiser ?? string.Empty, pattern, LikePattern.Escape)
                || EF.Functions.Like(calendarEvent.Venue ?? string.Empty, pattern, LikePattern.Escape));
        }

        return query;
    }

    private static IQueryable<Event> ApplyAdminFilters(
        IQueryable<Event> query,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        EventCategory? category,
        EventAdminFilter filter)
    {
        query = ApplyFilters(query, search, provinceId, districtId, category);

        return filter switch
        {
            EventAdminFilter.Published => query.Where(calendarEvent => calendarEvent.IsPublished),
            EventAdminFilter.Draft => query.Where(calendarEvent => !calendarEvent.IsPublished),
            _ => query,
        };
    }

    /// <summary>
    /// The sort is chosen from a fixed set and applied as a real <c>OrderBy</c>, never concatenated into
    /// a string. The identifier ends every ordering so that two events starting together keep a stable
    /// order between pages, which is what stops one appearing on both page 1 and page 2.
    private static IQueryable<Event> ApplyPublicOrder(EventSort sort, IQueryable<Event> query) => sort switch
    {
        EventSort.Newest => query
            .OrderByDescending(calendarEvent => calendarEvent.CreatedAt)
            .ThenBy(calendarEvent => calendarEvent.Id),

        _ => query
            .OrderBy(calendarEvent => calendarEvent.StartsAt)
            .ThenBy(calendarEvent => calendarEvent.Id),
    };

    private static void ApplyCommonFields(
        Event calendarEvent,
        SaveEventRequest request,
        Guid districtId,
        Guid? businessId)
    {
        calendarEvent.Slug = request.Slug.Trim();
        calendarEvent.Title = request.Title.Trim();
        calendarEvent.Summary = Blank(request.Summary);
        calendarEvent.Description = request.Description.Trim();
        calendarEvent.Category = request.Category;
        calendarEvent.Organiser = Blank(request.Organiser);
        calendarEvent.BusinessId = businessId;
        calendarEvent.DistrictId = districtId;
        calendarEvent.Venue = Blank(request.Venue);
        calendarEvent.RegistrationUrl = NormaliseRegistrationUrl(request.RegistrationUrl);
        calendarEvent.IsPublished = request.IsPublished;
    }

    private async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        await dbContext.Events
            .AnyAsync(calendarEvent => calendarEvent.Slug == slug.Trim(), cancellationToken);

    /// <summary>
    /// The district an event is in, after validation has already confirmed the code resolves.
    ///
    /// The throw is unreachable through the save path and is deliberately loud rather than a silent
    /// default: every caller of this runs <see cref="ValidateAsync"/> first, and the one place that could
    /// produce a null here is a future caller that forgets. Inventing a district would be worse than
    /// stopping, because it would put an event somewhere it is not.
    /// </summary>
    private async Task<Guid> ResolveRequiredDistrictAsync(string? districtCode, CancellationToken cancellationToken)
    {
        var districtId = await ResolveDistrictAsync(districtCode, cancellationToken);

        return districtId ?? throw new InvalidOperationException(
            "An event was saved without a resolvable district. Validation should have refused this first.");
    }

    /// <summary>
    /// The business running or hosting this, or null. Resolved by slug and deliberately not checked for
    /// publication: listing an event before its host business is listed is the normal order of work, and a
    /// slug that matches nothing is stored unlinked rather than refused.
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

    private async Task<Guid?> ResolveDistrictAsync(string? districtCode, CancellationToken cancellationToken)
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
    /// Everything that must be true before an event is worth showing a reader who may plan a trip
    /// around it, as field errors against the inputs the staff form actually has.
    ///
    /// This is the single source of the publish rules. The save path returns these as the reason a save
    /// was refused, and the staff editor returns the same list as <c>publishBlockers</c>, so what the
    /// editor tells you and what the server enforces cannot drift apart.
    internal IReadOnlyList<FieldError> CollectPublishBlockers(SaveEventRequest request)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            errors.Add(new FieldError(
                "summary",
                "required",
                "Write a one-line summary. It is what readers scan on the calendar, and without it the listing is a title with nothing under it."));
        }

        if (string.IsNullOrWhiteSpace(request.Venue))
        {
            errors.Add(new FieldError(
                "venue",
                "required",
                "Say where it is. A reader who has decided to go needs the venue, not just the district."));
        }

        if (request.Description.Trim().Length < MinPublishableDescription)
        {
            errors.Add(new FieldError(
                "description",
                "required",
                $"Write the details before publishing, at least {MinPublishableDescription} characters. Someone is deciding whether to leave the house on the strength of this."));
        }

        // A published event must not already have finished. Refusing here means an event that has ended
        // cannot be put back in front of readers without its times being corrected first, which is the
        // only order that makes sense.
        if (request.EndsAt is { } endsAt && endsAt < CurrentNepalDateTime())
        {
            errors.Add(new FieldError(
                "endsAt",
                "out_of_range",
                "This event has already finished, so it cannot be published. Move the times forward, or save it as a draft."));
        }

        return errors;
    }

    private async Task<IReadOnlyList<FieldError>> ValidateAsync(
        SaveEventRequest request,
        bool isGoingLive,
        CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.DistrictCode))
        {
            errors.Add(new FieldError(
                DistrictField,
                "required",
                "Choose the district this event is in, so readers browsing their own area can find it."));
        }
        else if (await ResolveDistrictAsync(request.DistrictCode, cancellationToken) is null)
        {
            errors.Add(new FieldError(
                DistrictField,
                "invalid",
                $"'{request.DistrictCode.Trim()}' is not a district this site knows about. Choose one from the list."));
        }

        if (!string.IsNullOrWhiteSpace(request.RegistrationUrl)
            && !ExternalUrlPolicy.TryNormalise(request.RegistrationUrl, RegistrationUrlField, out _, out var urlError))
        {
            errors.Add(new FieldError(RegistrationUrlField, "invalid", urlError!));
        }

        if (request.StartsAt is { } startsAt && request.EndsAt is { } endsAt && endsAt < startsAt)
        {
            errors.Add(new FieldError(
                "endsAt",
                "out_of_range",
                "The event ends before it starts. Move the end time later, or the start time earlier."));
        }

        // The ceiling is a publish rule rather than a save rule for the same reason the others are: a
        // staff member drafting a long-running thing should be able to keep typing, and only the
        // decision to publish it is refused.
        if (isGoingLive
            && request.StartsAt is { } publishStartsAt
            && request.EndsAt is { } publishEndsAt)
        {
            var hours = publishEndsAt - publishStartsAt;

            if (hours > TimeSpan.FromHours(MaxPublishableHours))
            {
                errors.Add(new FieldError(
                    "endsAt",
                    "out_of_range",
                    $"That runs for {hours.TotalDays:F0} days. A week is as long as one event is; anything longer should be several listings, so readers can see each one."));
            }
        }

        // Only when the event is actually going out. A live row being corrected may well need its times
        // moved in, and refusing that edit would make it impossible to bring a published event forward
        // through the screen that exists to do it.
        if (isGoingLive)
        {
            errors.AddRange(CollectPublishBlockers(request));
        }

        return errors;
    }

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

    private static string? NormaliseRegistrationUrl(string? raw) =>
        ExternalUrlPolicy.TryNormalise(raw, RegistrationUrlField, out var normalised, out _) ? normalised : null;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EventSummaryResponse ToSummaryResponse(Event calendarEvent, DateTime now) => new(
        calendarEvent.Id,
        calendarEvent.Slug,
        calendarEvent.Title,
        calendarEvent.Summary,
        calendarEvent.Category,
        calendarEvent.Organiser,
        calendarEvent.Business?.Name,
        calendarEvent.District?.Name ?? string.Empty,
        calendarEvent.District?.Province.Name ?? string.Empty,
        calendarEvent.Venue,
        calendarEvent.StartsAt,
        calendarEvent.EndsAt,
        IsOngoing(calendarEvent, now),
        calendarEvent.CreatedAt);

    private static EventDetailResponse ToDetailResponse(Event calendarEvent, DateTime now) => new(
        calendarEvent.Id,
        calendarEvent.Slug,
        calendarEvent.Title,
        calendarEvent.Summary,
        calendarEvent.Description,
        calendarEvent.Category,
        calendarEvent.Organiser,
        calendarEvent.Business?.Name,
        PublicBusinessSlug(calendarEvent.Business),
        calendarEvent.District?.Name ?? string.Empty,
        calendarEvent.District?.Province.Name ?? string.Empty,
        calendarEvent.Venue,
        calendarEvent.StartsAt,
        calendarEvent.EndsAt,
        IsOngoing(calendarEvent, now),
        calendarEvent.RegistrationUrl,
        calendarEvent.UpdatedAt);

    /// <summary>
    /// The business slug a reader may be given, or null when there is nothing to link to.
    /// <para>
    /// The organiser name is always reported, because an event whose host business has been retracted
    /// still has to say who was running it. The slug is a link, and a link to a business that is not
    /// listed leads nowhere: the business page answers 404 for a draft exactly as it does for a slug
    /// that never existed. Handing out the slug of an unlisted business would also leak the fact that
    /// some business by that name exists at all, which is the one thing a draft is supposed to hide.
    /// </para>
    /// </summary>
    private static string? PublicBusinessSlug(Business? business) =>
        business is { IsPublished: true } ? business.Slug : null;

    private EventAdminResponse ToAdminResponse(Event calendarEvent, string? author)
    {
        var district = calendarEvent.District;
        var province = district?.Province;
        var now = dates.NepalNow;

        return new EventAdminResponse(
            calendarEvent.Id,
            calendarEvent.Slug,
            calendarEvent.Title,
            calendarEvent.Summary,
            calendarEvent.Description,
            calendarEvent.Category,
            calendarEvent.Organiser,
            calendarEvent.Business?.Slug,
            district?.Code,
            district?.Name,
            province?.Code,
            province?.Name,
            calendarEvent.Venue,
            calendarEvent.StartsAt,
            calendarEvent.EndsAt,
            calendarEvent.RegistrationUrl,
            calendarEvent.IsPublished,
            calendarEvent.EndsAt < now,
            calendarEvent.CreatedAt,
            calendarEvent.UpdatedAt,
            author,
            calendarEvent.IsPublished
                ? []
                : CollectPublishBlockers(ToSaveRequest(calendarEvent)).Select(error => error.Message).ToList());
    }

    /// <summary>
    /// Re-projects a stored row back into the request shape so the publish rules can be evaluated against
    /// it. Without this, the editor's blocker list would need a second copy of every rule, and the two
    /// copies would eventually disagree.
    private static SaveEventRequest ToSaveRequest(Event calendarEvent) => new()
    {
        Slug = calendarEvent.Slug,
        Title = calendarEvent.Title,
        Summary = calendarEvent.Summary,
        Description = calendarEvent.Description,
        Category = calendarEvent.Category,
        Organiser = calendarEvent.Organiser,
        BusinessSlug = calendarEvent.Business?.Slug,
        DistrictCode = calendarEvent.District?.Code,
        Venue = calendarEvent.Venue,
        RegistrationUrl = calendarEvent.RegistrationUrl,
        StartsAt = calendarEvent.StartsAt,
        EndsAt = calendarEvent.EndsAt,
        IsPublished = calendarEvent.IsPublished,
    };

    /// <summary>
    /// Whether the event is under way at this moment, in Nepal local time. Derived on every read and
    /// stored nowhere, because "happening now" is not a property staff set: it stops being true at the
    /// finishing time on its own, and a stored flag would be wrong by the evening.
    /// </summary>
    private static bool IsOngoing(Event calendarEvent, DateTime now) =>
        calendarEvent.StartsAt <= now && now <= calendarEvent.EndsAt;

    /// <summary>
    /// The Nepal time the publish rules compare against. Read through the injected provider rather than
    /// the clock so a test can move the date without touching the machine's, which matters here more than
    /// on the offers page: the boundary between "still on" and "finished" is the minute.
    private DateTime CurrentNepalDateTime() => dates.NepalNow;

    /// <summary>
    /// MySQL error 1062 is ER_DUP_ENTRY, matching the job and business write paths. Catching every
    /// <see cref="DbUpdateException"/> instead would report a dead connection or a truncated column as a
    /// friendly "that slug is taken".
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}