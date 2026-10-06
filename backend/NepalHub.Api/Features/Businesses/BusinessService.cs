using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Ownership;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Students;
using NepalHub.Api.Features.Subscriptions;

namespace NepalHub.Api.Features.Businesses;

public enum BusinessSaveStatus
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

public enum BusinessDeleteStatus
{
    Deleted,
    NotFound,
    Published,
}

/// <summary>The validated, already-resolved form of a directory request.</summary>
public sealed record BusinessListQuery(
    PageRequest Paging,
    string? Search,
    Guid? ProvinceId,
    Guid? DistrictId,
    BusinessCategory? Category,
    bool? Verified,
    BusinessSort Sort);

public sealed record BusinessSaveOutcome(
    BusinessSaveStatus Status,
    BusinessAdminResponse? Business = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

/// <summary>
/// The resolved ids behind a location filter. Both are nullable because each filter is optional,
/// and a district wins over its province when both are given, which mirrors how the query reads a
/// narrower filter over a broader one.
/// <para>
/// <see cref="InvalidField"/> names the first argument whose code was not recognised, so the
/// controller can answer with a field-specific error rather than a generic "no results". Null means
/// everything given was understood.
/// </para>
/// </summary>
public sealed record BusinessLocationFilter(Guid? ProvinceId, Guid? DistrictId, string? InvalidField = null);

/// <summary>
/// Every rule for the business directory: browsing it as a reader and managing it as staff. The
/// controllers only map outcomes onto HTTP, as the quiz, calculator, resource and job services do.
///
/// Public visibility here is a single predicate, which is simpler than the job board because
/// nothing in a business directory expires. A listing stops being visible only when staff retract
/// it, and a retracted listing returns the same 404 as one that never existed so the directory
/// cannot be used to discover which slugs are real but not yet public.
/// </summary>
public sealed class BusinessService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    ILogger<BusinessService> logger)
{
    /// <summary>
    /// A description shorter than this on a published row is almost always a placeholder that was
    /// never finished. A directory of stubs is worse than a smaller directory: someone who walks
    /// across town to an address they cannot identify has spent their time, not ours.
    /// </summary>
    private const int MinPublishableDescription = 60;

    private const int MaxOpeningHourRows = 7;

    /// <summary>
    /// How many listings the paid strip carries. Small enough that it reads as a short
    /// recommendation rather than as a second directory, and capped in the query so the
    /// strip's cost does not grow with how many listings buy the slot.
    /// </summary>
    private const int FeaturedStripLimit = 6;

    private const string WebsiteField = "website";
    private const string PhoneField = "phone";
    private const string EmailField = "email";
    private const string HoursField = "openingHours";
    private const string DistrictField = "districtCode";

    /// <summary>
    /// The shortest and longest plausible Nepali subscriber number once the country code has been
    /// stripped. Seven digits is a landline, ten is a mobile; anything else is a typo. Deliberately
    /// loose about prefix and area codes, which are not settled, because a wrong rejection stops a
    /// legitimate business from being listed.
    /// </summary>
    private const int MinPhoneDigits = 7;
    private const int MaxPhoneDigits = 10;

    // ---------------------------------------------------------------- public read

    /// <summary>
    /// The directory. The published predicate is applied here and cannot be switched off from the
    /// outside, which is deliberate: an <c>includeUnpublished</c> flag on a public endpoint is a
    /// way to leak drafts by accident, and a flag nobody can turn on by accident is not a flag.
    /// </summary>
    internal async Task<PagedResult<BusinessSummaryResponse>> ListAsync(
        BusinessListQuery request,
        CancellationToken cancellationToken)
    {
        var now = Now();
        var query = ApplyFilters(
            PublicScope(),
            request.Search,
            request.ProvinceId,
            request.DistrictId,
            request.Category,
            request.Verified);

        var totalCount = await query.CountAsync(cancellationToken);
        var paged = request.Paging.ClampTo(totalCount);

        // The district and the week's hours are loaded for the page rather than projected in SQL,
        // because the card needs both the province name and a clock comparison. That is only two
        // extra joins on at most one page of rows, which is the trade this module accepts; the job
        // board projects in SQL because its summary carries neither.
        var items = await ApplyPublicOrder(request.Sort, query)
            .Skip(paged.Skip)
            .Take(paged.PageSize)
            .Include(business => business.District)
                .ThenInclude(district => district!.Province)
            .Include(business => business.OpeningHours)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var summaries = items
            .Select(business => ToSummaryResponse(business, now))
            .ToList();

        return new PagedResult<BusinessSummaryResponse>(summaries, paged.Page, paged.PageSize, totalCount);
    }

    /// <summary>
    /// The featured strip: at most six published, verified listings whose paid window covers
    /// today in Nepal, alphabetical. It sits above the directory rather than inside it — the
    /// main list keeps the order a reader understands, and the paid placement is honestly
    /// labelled instead of quietly rearranging what they came for.
    /// </summary>
    internal async Task<IReadOnlyList<BusinessSummaryResponse>> ListFeaturedAsync(
        CancellationToken cancellationToken)
    {
        var now = Now();
        var liveBusinessIds = SubscriptionRules.LiveBusinessIds(
            dbContext.BusinessSubscriptions,
            dates.Today);

        var items = await PublicScope()
            .Where(business => business.IsVerified && liveBusinessIds.Contains(business.Id))
            .OrderBy(business => business.Name)
            .ThenBy(business => business.Id)
            .Take(FeaturedStripLimit)
            .Include(business => business.District)
                .ThenInclude(district => district!.Province)
            .Include(business => business.OpeningHours)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return items
            .Select(business => ToSummaryResponse(business, now))
            .ToList();
    }

    /// <summary>
    /// One business as a reader sees it. Returns null for a draft and for a slug that never
    /// existed, identically, so this endpoint cannot be used to find out which slugs are real but not
    /// yet public. <paramref name="viewerId"/> only adds the caller's own ownership flag; it never
    /// widens what is visible, because the row itself still comes from the public scope.
    /// </summary>
    internal async Task<BusinessDetailResponse?> FindBySlugAsync(
        string slug,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var now = Now();

        var business = await PublicScope()
            .Where(item => item.Slug == slug)
            .Include(item => item.OpeningHours)
            .Include(item => item.District)
                .ThenInclude(district => district!.Province)
            .FirstOrDefaultAsync(cancellationToken);

        return business is null ? null : ToDetailResponse(business, now, viewerId);
    }

    /// <summary>
    /// Turns the province and district reference codes a caller sent into the ids the query needs.
    /// Codes rather than ids go over the wire because that is what the reference endpoints already
    /// publish, so the client never has to know a GUID, and a typo produces a specific
    /// <c>province_invalid</c> instead of silently matching nothing.
    ///
    /// A district that does not belong to the named province still filters to that district. The
    /// two are not cross-checked here: the filter is a narrowing, not a claim about the data, and
    /// the district is the more specific of the two. A listing cannot contradict its own location,
    /// because it only ever stores the district.
    /// </summary>
    internal async Task<BusinessLocationFilter> ResolveLocationFilterAsync(
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
                return new BusinessLocationFilter(null, null, "province");
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
                return new BusinessLocationFilter(provinceId, null, "district");
            }
        }

        return new BusinessLocationFilter(provinceId, districtId);
    }

    // ---------------------------------------------------------------- staff read

    /// <summary>
    /// The staff directory. Unlike the public list this shows drafts, because deciding what to
    /// retire is the whole point of the screen. The search, filters and paging are the same shape
    /// the reader uses, so there is one convention to learn.
    /// </summary>
    internal async Task<PagedResult<BusinessAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        BusinessCategory? category,
        bool? verified,
        BusinessAdminFilter filter,
        CancellationToken cancellationToken)
    {
        var query = ApplyAdminFilters(
            dbContext.Businesses.AsNoTracking(),
            search,
            provinceId,
            districtId,
            category,
            verified,
            filter);

        var totalCount = await query.CountAsync(cancellationToken);
        var paged = paging.ClampTo(totalCount);

        var items = await query
            .OrderByDescending(business => business.UpdatedAt)
            .ThenBy(business => business.Id)
            .Skip(paged.Skip)
            .Take(paged.PageSize)
            .Select(business => new BusinessAdminSummaryResponse(
                business.Id,
                business.Slug,
                business.Name,
                business.Category,
                business.District == null ? string.Empty : business.District.Name,
                business.IsPublished,
                business.IsVerified,
                business.UpdatedAt,
                business.CreatedByUser == null ? null : business.CreatedByUser.Email))
            .ToListAsync(cancellationToken);

        return new PagedResult<BusinessAdminSummaryResponse>(items, paged.Page, paged.PageSize, totalCount);
    }

    internal async Task<BusinessAdminResponse?> FindForAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .AsNoTracking()
            .Include(item => item.OpeningHours)
            .Include(item => item.District)
                .ThenInclude(district => district!.Province)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (business is null)
        {
            return null;
        }

        var author = business.CreatedByUserId is null
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == business.CreatedByUserId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);

        return ToAdminResponse(business, author);
    }

    // ---------------------------------------------------------------- staff writes

    internal async Task<BusinessSaveOutcome> CreateAsync(
        SaveBusinessRequest request,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, isGoingLive: request.IsPublished, cancellationToken);
        if (errors.Count > 0)
        {
            return new BusinessSaveOutcome(BusinessSaveStatus.Invalid, FieldErrors: errors);
        }

        if (await SlugExistsAsync(request.Slug, cancellationToken))
        {
            return new BusinessSaveOutcome(
                BusinessSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another listing.");
        }

        var districtId = await ResolveRequiredDistrictAsync(request.DistrictCode, cancellationToken);
        var website = NormaliseWebsite(request.Website);

        var business = new Business
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
        };

        ApplyCommonFields(business, request, districtId, website);
        ApplyOpeningHours(business, request.OpeningHours);

        dbContext.Businesses.Add(business);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index is the real guard. Two staff members saving the same slug at once is
            // an expected outcome, not a fault, and losing it is reported as the conflict it is
            // rather than as a server error.
            logger.LogInformation(exception, "A duplicate business slug was refused for {Slug}.", request.Slug);
            dbContext.Entry(business).State = EntityState.Detached;

            return new BusinessSaveOutcome(
                BusinessSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another listing.");
        }

        return new BusinessSaveOutcome(BusinessSaveStatus.Created, await FindForAdminAsync(business.Id, cancellationToken));
    }

    internal async Task<BusinessSaveOutcome> UpdateAsync(
        Guid id,
        SaveBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .Include(item => item.OpeningHours)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (business is null)
        {
            return new BusinessSaveOutcome(BusinessSaveStatus.NotFound);
        }

        // A published slug is the address the listing is live at, so it is frozen from that moment
        // on. A draft keeps its editable slug, because a staff member who mistyped it has not yet
        // sent anyone to the wrong URL and can still fix it.
        if (business.IsPublished
            && !string.Equals(business.Slug, request.Slug.Trim(), StringComparison.Ordinal))
        {
            return new BusinessSaveOutcome(
                BusinessSaveStatus.SlugImmutable,
                Message: "The slug cannot be changed once a listing is published, because that is the address people are already sharing. Unpublish it first if the address really has to change.");
        }

        // A row that is already public may be corrected in place, including having its badge taken
        // away, so the publish rules only apply to the transition from draft to live. Applying them
        // to every save of a published row would make unverifying impossible through the editor,
        // and unverifying is exactly what a live row with wrong details needs.
        var errors = await ValidateAsync(
            request,
            isGoingLive: request.IsPublished && !business.IsPublished,
            cancellationToken);
        if (errors.Count > 0)
        {
            return new BusinessSaveOutcome(BusinessSaveStatus.Invalid, FieldErrors: errors);
        }

        var trimmedSlug = request.Slug.Trim();
        if (!string.Equals(business.Slug, trimmedSlug, StringComparison.Ordinal)
            && await SlugExistsAsync(trimmedSlug, cancellationToken))
        {
            return new BusinessSaveOutcome(
                BusinessSaveStatus.SlugTaken,
                Message: $"The slug '{trimmedSlug}' is already used by another listing.");
        }

        var districtId = await ResolveRequiredDistrictAsync(request.DistrictCode, cancellationToken);
        var website = NormaliseWebsite(request.Website);

        ApplyCommonFields(business, request, districtId, website);

        // The whole set is replaced rather than diffed. Hours are at most seven rows whose meaning is
        // the schedule as a whole, so a partial update would only create ways for one day to fall out
        // of step with the other six.
        //
        // The removal is saved before the replacement is attached. A week's hours are keyed by
        // business and day, so a new Monday and the deleted Monday are the same key: keeping both
        // tracked at once makes the save fail with a tracking conflict rather than with anything
        // about the request. Two saves, and the row is never briefly a business with no hours.
        var previousHours = business.OpeningHours.ToList();
        dbContext.BusinessOpeningHours.RemoveRange(previousHours);
        business.OpeningHours.Clear();
        await dbContext.SaveChangesAsync(cancellationToken);

        ApplyOpeningHours(business, request.OpeningHours);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The same race the create path guards: the pre-check above narrows the window but does
            // not close it, and losing it is a conflict rather than a server error.
            logger.LogInformation(exception, "A duplicate business slug was refused for {Slug}.", trimmedSlug);
            dbContext.ChangeTracker.Clear();

            return new BusinessSaveOutcome(
                BusinessSaveStatus.SlugTaken,
                Message: $"The slug '{trimmedSlug}' is already used by another listing.");
        }

        return new BusinessSaveOutcome(BusinessSaveStatus.Updated, await FindForAdminAsync(id, cancellationToken));
    }

    /// <summary>
    /// Removes a draft, and its opening hours with it through the cascading foreign key. A published
    /// row is refused, because the intended operation is to retract it and hide it from readers, and
    /// deleting it instead loses the record of what was public.
    /// </summary>
    internal async Task<BusinessDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (business is null)
        {
            return BusinessDeleteStatus.NotFound;
        }

        if (business.IsPublished)
        {
            return BusinessDeleteStatus.Published;
        }

        dbContext.Businesses.Remove(business);
        await dbContext.SaveChangesAsync(cancellationToken);

        return BusinessDeleteStatus.Deleted;
    }

    // ---------------------------------------------------------------- owner writes

    /// <summary>
    /// One owned listing, for the owner's editor. Scoped by ownership in the query itself
    /// rather than by a check a caller could forget, so somebody else's listing answers the
    /// same null as an address that never existed and this cannot be used to probe for them.
    /// </summary>
    internal async Task<OwnerBusinessResponse?> FindOwnedAsync(
        string slug,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .Include(item => item.OpeningHours)
            .Include(item => item.District)
            .FirstOrDefaultAsync(
                item => item.Slug == slug && item.OwnerUserId == ownerUserId,
                cancellationToken);

        if (business is null)
        {
            return null;
        }

        // The owner's request for a check rides along with the editor so the panel can say
        // where it stands without a second round trip. The newest one wins: a rejected
        // request the owner has since corrected must not be what the screen reads, and a
        // second request can only exist once the first has been decided.
        var verification = await LatestVerificationAsync(business, cancellationToken);

        return ToOwnerResponse(business, verification);
    }

    private async Task<VerificationResponse?> LatestVerificationAsync(
        Business business,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.BusinessVerificationRequests
            .Where(item => item.BusinessId == business.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return request is null ? null : ToVerificationResponse(request, business);
    }

    /// <summary>
    /// Registers a listing on behalf of the person asking: the row is born owned, never
    /// published and never verified, because the only thing this endpoint grants is the
    /// authorship of a draft. Everything else — the badge, the live address — is what the
    /// verification queue exists to decide.
    ///
    /// <para>
    /// The draft goes through the same <see cref="ValidateAsync"/> the staff save runs, so a
    /// registration cannot smuggle in a district the site does not know or a phone number
    /// nobody could dial, and the slug rules (including the reserved <c>featured</c>) are
    /// the same ones staff are held to.
    /// </para>
    /// </summary>
    internal async Task<RegisterOutcome> RegisterAsync(
        RegisterBusinessRequest request,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var draft = request.AsDraft();
        var errors = await ValidateAsync(draft, isGoingLive: false, cancellationToken);
        if (errors.Count > 0)
        {
            return new RegisterOutcome(RegisterStatus.Invalid, FieldErrors: errors);
        }

        if (await SlugExistsAsync(request.Slug, cancellationToken))
        {
            return new RegisterOutcome(
                RegisterStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another listing.");
        }

        var districtId = await ResolveRequiredDistrictAsync(request.DistrictCode, cancellationToken);
        var website = NormaliseWebsite(request.Website);

        var business = new Business
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = ownerUserId,
            OwnerUserId = ownerUserId,
        };

        ApplyCommonFields(business, draft, districtId, website);
        ApplyOpeningHours(business, draft.OpeningHours);

        dbContext.Businesses.Add(business);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index is the real guard. A second registration of the same slug in
            // the same instant is an expected outcome, not a fault, and losing it is
            // reported as the conflict it is rather than as a server error.
            logger.LogInformation(exception, "A duplicate business slug was refused for {Slug}.", request.Slug);
            dbContext.Entry(business).State = EntityState.Detached;

            return new RegisterOutcome(
                RegisterStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another listing.");
        }

        return new RegisterOutcome(
            RegisterStatus.Created,
            await FindOwnedAsync(business.Slug, ownerUserId, cancellationToken));
    }

    /// <summary>
    /// The owner's edit: contact details, description, opening hours, and — while the row is
    /// still a draft — the identity fields they typed when registering. No publish flag and
    /// no verify badge in any state, because an editor that lets an owner self-publish would
    /// erase the line this whole module exists to keep.
    ///
    /// <para>
    /// The identity fields follow the staff editor's own rule: a draft's slug and name can be
    /// corrected because nobody has been sent to that address yet, and a published row's
    /// cannot, because after that the slug is a live address people and search engines have
    /// recorded. A retracted row is a draft again by the same rule the staff save uses.
    /// </para>
    /// </summary>
    internal async Task<OwnerSaveOutcome> UpdateOwnedAsync(
        string slug,
        Guid ownerUserId,
        SaveOwnerBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .Include(item => item.OpeningHours)
            .FirstOrDefaultAsync(
                item => item.Slug == slug && item.OwnerUserId == ownerUserId,
                cancellationToken);

        if (business is null)
        {
            return new OwnerSaveOutcome(OwnerEditStatus.NotFound);
        }

        var errors = new List<FieldError>(ValidateOwnerEdit(request));
        errors.AddRange(await ValidateOwnerIdentityAsync(business, request, cancellationToken));

        if (errors.Count > 0)
        {
            return new OwnerSaveOutcome(OwnerEditStatus.Invalid, FieldErrors: errors);
        }

        if (request.Slug is not null)
        {
            business.Slug = request.Slug.Trim();
        }

        if (request.Name is not null)
        {
            business.Name = request.Name.Trim();
        }

        if (request.Category is { } category)
        {
            business.Category = category;
        }

        if (request.DistrictCode is not null)
        {
            business.DistrictId = await ResolveRequiredDistrictAsync(request.DistrictCode, cancellationToken);
        }

        business.Description = request.Description.Trim();
        business.Address = Blank(request.Address);
        business.Phone = Blank(request.Phone);
        business.Email = Blank(request.Email);
        business.Website = NormaliseWebsite(request.Website);

        // The same two-save replacement the staff save uses. Hours are keyed by business and
        // day, so a deleted Monday and a new Monday are the same key: keeping both tracked at
        // once fails with a tracking conflict rather than with anything about the request,
        // and the removal is what makes room for the week as a whole.
        var previousHours = business.OpeningHours.ToList();
        dbContext.BusinessOpeningHours.RemoveRange(previousHours);
        business.OpeningHours.Clear();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index is the real guard: the slug pre-check above narrows the
            // window between two owners renaming to the same address, and losing the race
            // is a conflict rather than a server error.
            logger.LogInformation(exception, "A duplicate business slug was refused for {Slug}.", business.Slug);
            dbContext.ChangeTracker.Clear();

            return new OwnerSaveOutcome(
                OwnerEditStatus.SlugTaken,
                Message: $"The slug '{business.Slug}' is already used by another listing.");
        }

        ApplyOpeningHours(business, request.OpeningHours);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new OwnerSaveOutcome(
            OwnerEditStatus.Updated,
            await FindOwnedByIdAsync(business.Id, cancellationToken));
    }

    private async Task<OwnerBusinessResponse?> FindOwnedByIdAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .Include(item => item.OpeningHours)
            .Include(item => item.District)
            .FirstOrDefaultAsync(item => item.Id == businessId, cancellationToken);

        if (business is null)
        {
            return null;
        }

        var verification = await LatestVerificationAsync(business, cancellationToken);

        return ToOwnerResponse(business, verification);
    }

    /// <summary>
    /// The identity half of an owner's edit: what they typed, held to the same rules the
    /// staff editor is held to. A published row answers with an <c>immutable</c> field error
    /// naming the field rather than silently ignoring it, because an owner who corrected a
    /// name and was told nothing would keep correcting it.
    /// </summary>
    private async Task<IReadOnlyList<FieldError>> ValidateOwnerIdentityAsync(
        Business business,
        SaveOwnerBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();

        if (business.IsPublished)
        {
            if (request.Slug is not null
                && !string.Equals(business.Slug, request.Slug.Trim(), StringComparison.Ordinal))
            {
                errors.Add(new FieldError(
                    "slug",
                    "immutable",
                    "The slug cannot be changed once a listing is published, because that is the address people are already sharing."));
            }

            if (request.Name is not null
                && !string.Equals(business.Name, request.Name.Trim(), StringComparison.Ordinal))
            {
                errors.Add(new FieldError(
                    "name",
                    "immutable",
                    "The name cannot be changed once a listing is published. Ask staff to correct it."));
            }

            if (request.Category is { } category && category != business.Category)
            {
                errors.Add(new FieldError(
                    "category",
                    "immutable",
                    "The category cannot be changed once a listing is published. Ask staff to correct it."));
            }

            if (request.DistrictCode is not null
                && await ResolveDistrictAsync(request.DistrictCode, cancellationToken) != business.DistrictId)
            {
                errors.Add(new FieldError(
                    "districtCode",
                    "immutable",
                    "The district cannot be changed once a listing is published. Ask staff to correct it."));
            }

            return errors;
        }

        if (request.Slug is not null)
        {
            var trimmedSlug = request.Slug.Trim();

            if (trimmedSlug.Equals("featured", StringComparison.Ordinal))
            {
                errors.Add(new FieldError(
                    "slug",
                    "reserved",
                    "'featured' is a reserved address on this site. Choose another slug."));
            }
            else if (await SlugExistsAsync(trimmedSlug, business.Id, cancellationToken))
            {
                errors.Add(new FieldError(
                    "slug",
                    "taken",
                    $"The slug '{trimmedSlug}' is already used by another listing."));
            }
        }

        if (request.DistrictCode is not null
            && await ResolveDistrictAsync(request.DistrictCode, cancellationToken) is null)
        {
            errors.Add(new FieldError(
                "districtCode",
                "invalid",
                $"'{request.DistrictCode.Trim()}' is not a district this site knows about. Choose one from the list."));
        }

        return errors;
    }

    /// <summary>
    /// The shape and set rules for what an owner may write. The same helpers the staff path
    /// uses, so an owner and staff member are held to one definition of a phone number, an
    /// address line and a week of hours rather than to two that drift.
    /// </summary>
    private static IReadOnlyList<FieldError> ValidateOwnerEdit(SaveOwnerBusinessRequest request)
    {
        var errors = new List<FieldError>();

        if (!string.IsNullOrWhiteSpace(request.Website)
            && !ExternalUrlPolicy.TryNormalise(request.Website, WebsiteField, out _, out var urlError))
        {
            errors.Add(new FieldError(WebsiteField, "invalid", urlError!));
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !IsEmailShaped(request.Email))
        {
            errors.Add(new FieldError(
                EmailField,
                "invalid",
                "That does not look like an email address. Check it for a missing @ or a missing dot in the domain."));
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && !TryNormalisePhone(request.Phone, out _, out var phoneError))
        {
            errors.Add(new FieldError(PhoneField, "invalid", phoneError!));
        }

        errors.AddRange(ValidateOpeningHours(request.OpeningHours));

        return errors;
    }

    private static OwnerBusinessResponse ToOwnerResponse(
        Business business,
        VerificationResponse? verification) => new(
        business.Id,
        business.Slug,
        business.Name,
        business.Description,
        business.Category,
        business.District?.Code ?? string.Empty,
        business.District?.Name ?? string.Empty,
        business.Address,
        business.Phone,
        business.Email,
        business.Website,
        business.IsPublished,
        business.IsVerified,
        ToHoursResponses(business),
        business.UpdatedAt,
        verification);

    private static VerificationResponse ToVerificationResponse(
        BusinessVerificationRequest request,
        Business? business) => new(
        request.Id,
        request.BusinessId,
        business?.Slug ?? request.Business?.Slug ?? string.Empty,
        business?.Name ?? request.Business?.Name ?? string.Empty,
        request.Status,
        request.Note,
        request.CreatedAt,
        request.ReviewedAt,
        request.ReviewNote);

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// Published and nothing else. There is no expiry rule to pair with it: a restaurant does not
    /// stop existing because its deadline passed, so the second predicate the job board needs has
    /// no equivalent here.
    /// </summary>
    private IQueryable<Business> PublicScope() =>
        NepalHub.Api.Data.PublicContentScope.Businesses(dbContext.Businesses);

    private static IQueryable<Business> ApplyFilters(
        IQueryable<Business> query,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        BusinessCategory? category,
        bool? verified)
    {
        if (districtId is { } selectedDistrict)
        {
            query = query.Where(business => business.DistrictId == selectedDistrict);
        }
        else if (provinceId is { } selectedProvince)
        {
            // A province filter is a filter on the district's province, not a column on the row.
            // There is no province_id on businesses precisely so the two cannot disagree, and this
            // is what that decision costs: one join in the filter, rather than two redundant columns.
            query = query.Where(business => business.District != null && business.District.ProvinceId == selectedProvince);
        }

        if (category is { } selectedCategory)
        {
            query = query.Where(business => business.Category == selectedCategory);
        }

        if (verified is { } isVerified)
        {
            query = query.Where(business => business.IsVerified == isVerified);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());

            // Name, description and address only. Phone and email are deliberately left out: a
            // keyword search over contact details turns the directory into a way to enumerate the
            // phone numbers of small businesses one digit pattern at a time.
            query = query.Where(business =>
                EF.Functions.Like(business.Name, pattern, LikePattern.Escape)
                || EF.Functions.Like(business.Description, pattern, LikePattern.Escape)
                || EF.Functions.Like(business.Address ?? string.Empty, pattern, LikePattern.Escape));
        }

        return query;
    }

    private static IQueryable<Business> ApplyAdminFilters(
        IQueryable<Business> query,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        BusinessCategory? category,
        bool? verified,
        BusinessAdminFilter filter)
    {
        query = ApplyFilters(query, search, provinceId, districtId, category, verified);

        return filter switch
        {
            BusinessAdminFilter.Published => query.Where(business => business.IsPublished),
            BusinessAdminFilter.Draft => query.Where(business => !business.IsPublished),
            _ => query,
        };
    }

    /// <summary>
    /// The sort is chosen from a fixed set and applied as a real <c>OrderBy</c>, never
    /// concatenated into a string. The identifier ends every ordering so that two rows sharing a
    /// creation instant keep a stable order between pages, which is what stops a listing appearing
    /// on both page 1 and page 2.
    /// <para>
    /// There is deliberately no "verified first" ordering. Ranking the checked businesses above the
    /// unchecked ones is a promotion decision, and featured placement is not something this phase
    /// offers; an alphabetical list that a reader understands is worth more than one that looks like
    /// an advert.
    /// </para>
    /// </summary>
    private static IQueryable<Business> ApplyPublicOrder(BusinessSort sort, IQueryable<Business> query) => sort switch
    {
        BusinessSort.Name => query
            .OrderBy(business => business.Name)
            .ThenBy(business => business.Id),

        _ => query
            .OrderByDescending(business => business.CreatedAt)
            .ThenBy(business => business.Id),
    };

    private static void ApplyCommonFields(
        Business business,
        SaveBusinessRequest request,
        Guid districtId,
        string? website)
    {
        business.Slug = request.Slug.Trim();
        business.Name = request.Name.Trim();
        business.Description = request.Description.Trim();
        business.Category = request.Category;
        business.DistrictId = districtId;
        business.Address = Blank(request.Address);
        business.Phone = Blank(request.Phone);
        business.Email = Blank(request.Email);
        business.Website = website;
        business.IsPublished = request.IsPublished;
        business.IsVerified = request.IsVerified;

        // The timestamp is written here rather than stored as a flag, so the staff list can show how
        // long a listing has gone unchecked. VerifiedAt is cleared when the badge comes off, so a
        // staff member cannot see a check date for a listing that is not currently checked.
        business.VerifiedAt = request.IsVerified ? DateTime.UtcNow : null;
    }

    private static void ApplyOpeningHours(Business business, IReadOnlyList<SaveBusinessHoursRequest> hours)
    {
        foreach (var day in hours)
        {
            business.OpeningHours.Add(new BusinessOpeningHours
            {
                BusinessId = business.Id,
                DayOfWeek = day.DayOfWeek,
                IsClosed = day.IsClosed,
                OpensAt = day.IsClosed ? null : day.OpensAt,
                ClosesAt = day.IsClosed ? null : day.ClosesAt,
            });
        }
    }

    private async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        await SlugExistsAsync(slug, exceptBusinessId: null, cancellationToken);

    private async Task<bool> SlugExistsAsync(
        string slug,
        Guid? exceptBusinessId,
        CancellationToken cancellationToken) =>
        await dbContext.Businesses
            .AnyAsync(
                business => business.Slug == slug.Trim()
                    && (exceptBusinessId == null || business.Id != exceptBusinessId),
                cancellationToken);

    /// <summary>
    /// The district a listing is about, after validation has already confirmed the code resolves.
    ///
    /// The throw is unreachable through the save path and is deliberately loud rather than a silent
    /// default: every caller of this runs <see cref="ValidateAsync"/> first, and the one place that
    /// could produce a null here is a future caller that forgets. Inventing a district would be worse
    /// than stopping, because it would put a listing somewhere it is not.
    /// </summary>
    private async Task<Guid> ResolveRequiredDistrictAsync(string? districtCode, CancellationToken cancellationToken)
    {
        var districtId = await ResolveDistrictAsync(districtCode, cancellationToken);

        return districtId ?? throw new InvalidOperationException(
            "A business was saved without a resolvable district. Validation should have refused this first.");
    }

    /// <summary>
    /// Resolves a district reference code to its id, returning null for a blank or unknown code.
    /// </summary>
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
    /// Everything that must be true before a listing is worth showing a reader who may walk across
    /// town on the strength of it, as field errors against the inputs the staff form actually has.
    ///
    /// This is the single source of the publish rules. The save path returns these as the reason a
    /// save was refused, and the staff editor returns the same list as <c>publishBlockers</c>, so
    /// what the editor tells you and what the server enforces cannot drift apart.
    /// </summary>
    internal static IReadOnlyList<FieldError> CollectPublishBlockers(SaveBusinessRequest request)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            errors.Add(new FieldError(
                PhoneField,
                "required",
                "A published listing needs a phone number. Someone reading this will call before they travel, and a directory entry they cannot call is the one they will not use."));
        }

        if (request.Description.Trim().Length < MinPublishableDescription)
        {
            errors.Add(new FieldError(
                "description",
                "required",
                $"Write at least {MinPublishableDescription} characters before publishing. Someone is deciding whether to spend their morning travelling here, and three lines does not tell them what they are walking into."));
        }

        // Publishing requires verification, and this is where that rule lives. The two flags are
        // independent in storage, so this is a publish-path rule rather than a constraint on the
        // columns: unverifying later leaves the listing public, but publishing an unchecked one is
        // not possible at all.
        if (!request.IsVerified)
        {
            errors.Add(new FieldError(
                "isVerified",
                "required",
                "Verify this listing before publishing it, so it goes out with the badge it should carry."));
        }

        return errors;
    }

    private async Task<IReadOnlyList<FieldError>> ValidateAsync(
        SaveBusinessRequest request,
        bool isGoingLive,
        CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();

        // The featured strip is served from this route as a literal segment, and a literal
        // beats a parameter, so a listing at /api/businesses/featured would be shadowed by
        // it. Reserving the word costs this one check and keeps both addresses readable.
        if (request.Slug.Trim().Equals("featured", StringComparison.Ordinal))
        {
            errors.Add(new FieldError(
                "slug",
                "reserved",
                "'featured' is a reserved address on this site. Choose another slug."));
        }

        if (string.IsNullOrWhiteSpace(request.DistrictCode))
        {
            errors.Add(new FieldError(
                DistrictField,
                "required",
                "Choose the district this business is in, so readers browsing their own area can find it."));
        }
        else if (await ResolveDistrictAsync(request.DistrictCode, cancellationToken) is null)
        {
            errors.Add(new FieldError(
                DistrictField,
                "invalid",
                $"'{request.DistrictCode.Trim()}' is not a district this site knows about. Choose one from the list."));
        }

        if (!string.IsNullOrWhiteSpace(request.Website)
            && !ExternalUrlPolicy.TryNormalise(request.Website, WebsiteField, out _, out var urlError))
        {
            errors.Add(new FieldError(WebsiteField, "invalid", urlError!));
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !IsEmailShaped(request.Email))
        {
            errors.Add(new FieldError(
                EmailField,
                "invalid",
                "That does not look like an email address. Check it for a missing @ or a missing dot in the domain."));
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && !TryNormalisePhone(request.Phone, out _, out var phoneError))
        {
            errors.Add(new FieldError(PhoneField, "invalid", phoneError!));
        }

        errors.AddRange(ValidateOpeningHours(request.OpeningHours));

        // Only when the listing is actually going out. A live row that is being corrected may well
        // lose its badge, and refusing that edit would mean the one state the directory exists to be
        // honest about could never be reached through the screen that sets it.
        if (isGoingLive)
        {
            errors.AddRange(CollectPublishBlockers(request));
        }

        return errors;
    }

    /// <summary>
    /// The same publish rules, asked about a stored row instead of a form post. The
    /// verification queue runs this on approval, so "staff approved this listing" and "this
    /// listing satisfies the publish rules" are the same statement rather than two that can
    /// drift apart.
    ///
    /// <para>
    /// <c>IsVerified</c> is projected as true because verification is precisely what an
    /// approval is doing; the rules that still have to hold are the ones about the listing
    /// itself — a number to call and something worth reading.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<FieldError> CollectPublishBlockersFor(Business business) =>
        CollectPublishBlockers(new SaveBusinessRequest
        {
            Slug = business.Slug,
            Name = business.Name,
            Description = business.Description,
            Category = business.Category,
            DistrictCode = business.DistrictId.ToString(),
            Address = business.Address,
            Phone = business.Phone,
            Email = business.Email,
            Website = business.Website,
            IsPublished = business.IsPublished,
            IsVerified = true,
            OpeningHours = [],
        });

    /// <summary>
    /// The hours rules, kept apart from the field-level checks because they are the one place in this
    /// save where the whole set has to be judged rather than one input at a time: two rows for the
    /// same day is only wrong in the context of the other six.
    /// </summary>
    private static IReadOnlyList<FieldError> ValidateOpeningHours(IReadOnlyList<SaveBusinessHoursRequest> hours)
    {
        var errors = new List<FieldError>();

        if (hours.Count > MaxOpeningHourRows)
        {
            errors.Add(new FieldError(
                HoursField,
                "out_of_range",
                $"List at most {MaxOpeningHourRows} days, one for each day of the week."));

            // Reported once. Every further complaint would be about rows the first error already
            // rules out, and a staff member should see one thing to fix.
            return errors;
        }

        var seen = new HashSet<DayOfWeek>();

        foreach (var day in hours)
        {
            if (!seen.Add(day.DayOfWeek))
            {
                errors.Add(new FieldError(
                    HoursField,
                    "duplicate",
                    $"{day.DayOfWeek} is listed more than once. Each day can appear only once."));

                continue;
            }

            if (day.IsClosed)
            {
                continue;
            }

            if (day.OpensAt is null || day.ClosesAt is null)
            {
                errors.Add(new FieldError(
                    HoursField,
                    "required",
                    $"{day.DayOfWeek} is not marked closed, so it needs both an opening and a closing time."));

                continue;
            }

            if (day.OpensAt == day.ClosesAt)
            {
                errors.Add(new FieldError(
                    HoursField,
                    "out_of_range",
                    $"{day.DayOfWeek} opens and closes at the same time, so it is not open at all. Mark the day closed instead."));
            }

            // A closing time earlier than the opening time is not refused: it means the shift runs
            // past midnight, which is ordinary for a bar or a bakery and would otherwise leave the
            // businesses most likely to publish hours unable to publish them.
        }

        return errors;
    }

    /// <summary>
    /// Reduces a typed number to digits, dropping the punctuation people write and an optional 977
    /// country code, and refuses anything left with too few or too many digits to be a Nepali
    /// number.
    ///
    /// Prefix and area codes are not checked. Those conventions are not settled enough to refuse a
    /// real number over, and the cost of a wrong rejection is higher than the cost of a wrong
    /// number being stored: the reader hears a wrong number, not a wrong business.
    /// </summary>
    public static bool TryNormalisePhone(string? raw, out string? normalised, out string? error)
    {
        normalised = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        // Only the punctuation people write between digits is dropped. Anything else is a reason to
        // stop: silently discarding letters would turn "abc1234567" into a valid number, so a
        // mistyped field would be stored looking checked rather than rejected while it is being
        // corrected.
        const string AllowedPunctuation = " +-()";

        if (raw.Any(character => !char.IsAsciiDigit(character)
            && !AllowedPunctuation.Contains(character, StringComparison.Ordinal)))
        {
            error = "A phone number is digits only. Leave out anything else, or separate the digits with spaces, a plus, a dash or brackets.";

            return false;
        }

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());

        if (digits.StartsWith("977", StringComparison.Ordinal) && digits.Length > MinPhoneDigits)
        {
            digits = digits[3..];
        }

        if (digits.Length is < MinPhoneDigits or > MaxPhoneDigits)
        {
            error = $"Enter a Nepali phone number: 7 to {MaxPhoneDigits} digits, optionally starting with 977 or 0.";

            return false;
        }

        normalised = digits;

        return true;
    }

    /// <summary>
    /// A shape check on an optional contact address. Not an RFC 5322 parser: the full grammar accepts
    /// forms no mail server ever issued, and one dependency to accept a string a person can read back
    /// to themselves is not worth carrying. There is no mail server behind this product, so this
    /// value is never verified by sending to it either.
    /// </summary>
    private static bool IsEmailShaped(string raw)
    {
        var trimmed = raw.Trim();
        var separator = trimmed.IndexOf('@');

        if (separator <= 0 || separator != trimmed.LastIndexOf('@'))
        {
            return false;
        }

        var domain = trimmed[(separator + 1)..];

        return domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal);
    }

    private static string? NormaliseWebsite(string? raw) =>
        ExternalUrlPolicy.TryNormalise(raw, WebsiteField, out var normalised, out _) ? normalised : null;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BusinessSummaryResponse ToSummaryResponse(Business business, DateTime now) => new(
        business.Id,
        business.Slug,
        business.Name,
        business.Category,
        business.District?.Name ?? string.Empty,
        business.District?.Province.Name ?? string.Empty,
        business.Address,
        business.Phone,
        business.IsVerified,
        IsOpenNow(business, now),
        business.CreatedAt);

    private static BusinessDetailResponse ToDetailResponse(Business business, DateTime now, Guid? viewerId) => new(
        business.Id,
        business.Slug,
        business.Name,
        business.Description,
        business.Category,
        business.District?.Name ?? string.Empty,
        business.District?.Province.Name ?? string.Empty,
        business.Address,
        business.Phone,
        business.Email,
        business.Website,
        business.IsVerified,
        IsOpenNow(business, now),
        ToHoursResponses(business),
        business.UpdatedAt,
        viewerId is not null && business.OwnerUserId == viewerId);

    /// <summary>
    /// Hours as an ordered week, always Sunday first, whether or not the business listed that day.
    /// A reader wants to see the whole week in one glance, so a missing day is shown as closed
    /// rather than silently absent — with the exception of a listing that published no hours at all,
    /// which reports an empty list so the page can say "hours not listed" rather than claiming a
    /// schedule nobody supplied.
    /// </summary>
    private static IReadOnlyList<BusinessHoursResponse> ToHoursResponses(Business business)
    {
        if (business.OpeningHours.Count == 0)
        {
            return [];
        }

        var byDay = business.OpeningHours.ToDictionary(hours => hours.DayOfWeek);

        return
        [
            .. Enum.GetValues<DayOfWeek>()
                .OrderBy(day => (int)day)
                .Select(day => byDay.TryGetValue(day, out var hours) && !hours.IsClosed
                    ? new BusinessHoursResponse(day, false, hours.OpensAt, hours.ClosesAt)
                    : new BusinessHoursResponse(day, true, null, null)),
        ];
    }

    /// <summary>
    /// Whether the business is open at this moment, in Nepal local time. Null when it published no
    /// hours at all, which is a different statement from "closed": one is unknown and the other is
    /// a fact, and a card that shows a red closed badge for a business that simply never listed its
    /// hours is telling the reader something untrue.
    /// </summary>
    private static bool? IsOpenNow(Business business, DateTime now)
    {
        if (business.OpeningHours.Count == 0)
        {
            return null;
        }

        var time = TimeOnly.FromDateTime(now);
        var today = business.OpeningHours.FirstOrDefault(hours => hours.DayOfWeek == now.DayOfWeek);

        if (today is not null && today.IsClosed)
        {
            // A day the business closed on its own is a fact about that whole day, including the
            // hours after midnight that belong to it, so yesterday's overnight shift does not
            // override it.
            return false;
        }

        if (today is not null && today.OpensAt is not null && today.ClosesAt is not null)
        {
            // A window that closes before it opens has run past midnight. Before today's opening
            // time only last night's shift can still be running, and it is checked below against
            // yesterday's row rather than assumed from today's.
            if (today.ClosesAt > today.OpensAt)
            {
                return time >= today.OpensAt && time < today.ClosesAt;
            }

            if (time >= today.OpensAt)
            {
                return true;
            }
        }

        // Yesterday is only consulted for the small hours. Its closing time has already passed by
        // the time a same-day schedule would open, so a window that does not cross midnight can
        // never match here.
        var yesterday = business.OpeningHours.FirstOrDefault(hours => hours.DayOfWeek == Yesterday(now));

        if (yesterday is null || yesterday.IsClosed || yesterday.OpensAt is null || yesterday.ClosesAt is null)
        {
            return false;
        }

        return yesterday.ClosesAt <= yesterday.OpensAt && time < yesterday.ClosesAt;
    }

    private BusinessAdminResponse ToAdminResponse(Business business, string? author)
    {
        var district = business.District;
        var province = district?.Province;

        return new BusinessAdminResponse(
            business.Id,
            business.Slug,
            business.Name,
            business.Description,
            business.Category,
            district?.Code ?? string.Empty,
            district?.Name ?? string.Empty,
            province?.Code ?? string.Empty,
            province?.Name ?? string.Empty,
            business.Address,
            business.Phone,
            business.Email,
            business.Website,
            business.IsPublished,
            business.IsVerified,
            business.VerifiedAt,
            ToHoursResponses(business),
            business.CreatedAt,
            business.UpdatedAt,
            author,
            business.IsPublished
                ? []
                : CollectPublishBlockers(ToSaveRequest(business)).Select(error => error.Message).ToList());
    }

    /// <summary>
    /// Re-projects a stored row back into the request shape so the publish rules can be evaluated
    /// against it. Without this, the editor's blocker list would need a second copy of every rule,
    /// and the two copies would eventually disagree.
    /// </summary>
    private static SaveBusinessRequest ToSaveRequest(Business business) => new()
    {
        Slug = business.Slug,
        Name = business.Name,
        Description = business.Description,
        Category = business.Category,
        DistrictCode = business.District?.Code,
        Address = business.Address,
        Phone = business.Phone,
        Email = business.Email,
        Website = business.Website,
        IsPublished = business.IsPublished,
        IsVerified = business.IsVerified,
    };

    /// <summary>
    /// Nepal local time, because "is it open now" has to be answered against the clock on the shop's
    /// wall rather than UTC. UTC+05:45 against a nine-to-six schedule is not a rounding detail: it
    /// shifts every answer by nearly six hours.
    /// </summary>
    private DateTime Now() => dates.NepalNow;

    /// <summary>
    /// The weekday before the one given, wrapping Sunday back to Saturday.
    /// </summary>
    private static DayOfWeek Yesterday(DateTime date) =>
        date.DayOfWeek == DayOfWeek.Sunday ? DayOfWeek.Saturday : date.DayOfWeek - 1;

    /// <summary>
    /// MySQL error 1062 is ER_DUP_ENTRY, matching the job and resource write paths. Catching every
    /// <see cref="DbUpdateException"/> instead would report a dead connection or a truncated
    /// column as a friendly "that slug is taken".
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}