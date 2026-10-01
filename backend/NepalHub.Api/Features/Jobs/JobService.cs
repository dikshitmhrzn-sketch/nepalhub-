using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Students;

namespace NepalHub.Api.Features.Jobs;

public enum JobSaveStatus
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

public enum JobDeleteStatus
{
    Deleted,
    NotFound,
    Published,
}

/// <summary>The validated, already-resolved form of a board request.</summary>
public sealed record JobListQuery(
    PageRequest Paging,
    string? Search,
    Guid? ProvinceId,
    Guid? DistrictId,
    JobCategory? Category,
    EmploymentType? EmploymentType,
    WorkMode? WorkMode,
    JobSort Sort);

public sealed record JobSaveOutcome(
    JobSaveStatus Status,
    JobAdminResponse? Job = null,
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
public sealed record JobLocationFilter(Guid? ProvinceId, Guid? DistrictId, string? InvalidField = null);

/// <summary>
/// Every rule for the job board: browsing it as a reader and managing it as staff. The controllers
/// only map outcomes onto HTTP, as the quiz, calculator and resource services do.
///
/// The rule that shapes most of this file is that published and unexpired are separate concerns
/// that always travel together on the public path. A draft is invisible because nobody has
/// approved it; a closed posting is invisible because it is worse than nothing to an applicant who
/// acts on it and finds the employer's form gone. Both produce the same answer to a reader, and
/// the same 404, so neither can be probed.
/// </summary>
public sealed class JobService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    ILogger<JobService> logger)
{
    /// <summary>
    /// A posting body shorter than this on a published row is almost always a placeholder that
    /// was never finished, and a board of stubs is worse than a smaller board: an applicant who
    /// clicks through and finds three lines has been sent to their time's cost, not ours.
    /// </summary>
    private const int MinPublishableDescription = 80;

    /// <summary>
    /// How far ahead a deadline counts as closing soon. Three days is the window in which an
    /// applicant who has not applied yet can still realistically be told to hurry, which is the
    /// whole value of the badge.
    /// </summary>
    private const int ClosingSoonWindowDays = 3;

    private const string ApplicationUrlField = "applicationUrl";

    // ---------------------------------------------------------------- public read

    /// <summary>
    /// The board. The published and not-closed predicates are applied here and cannot be switched
    /// off from the outside, which is deliberate: an <c>includeUnpublished</c> flag on a public
    /// endpoint is a way to leak drafts by accident, and a flag nobody can turn on by accident is
    /// not a flag.
    /// </summary>
    internal async Task<PagedResult<JobSummaryResponse>> ListAsync(
        JobListQuery request,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var query = ApplyFilters(
            PublicScope(today),
            request.Search,
            request.ProvinceId,
            request.DistrictId,
            request.Category,
            request.EmploymentType,
            request.WorkMode);

        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = ApplyPublicOrder(request.Sort, query);

        var items = await ordered
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .Select(job => new JobSummaryResponse(
                job.Id,
                job.Slug,
                job.Title,
                job.Organization,
                job.Category,
                job.EmploymentType,
                job.WorkMode,
                job.District == null ? null : job.District.Name,
                job.District == null ? null : job.District.Province.Name,
                job.ApplicationDeadline,
                job.ApplicationDeadline <= today.AddDays(ClosingSoonWindowDays)
                    ? JobStatus.ClosingSoon
                    : JobStatus.Open,
                job.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<JobSummaryResponse>(items, request.Paging.Page, request.Paging.PageSize, totalCount);
    }

    /// <summary>
    /// One posting as a reader sees it. Returns null for a draft, a closed posting, and a slug that
    /// never existed, all identically, so this endpoint cannot be used to find out which slugs are
    /// real but not yet public.
    /// </summary>
    internal async Task<JobDetailResponse?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var today = dates.Today;

        return await PublicScope(today)
            .Where(job => job.Slug == slug)
            .Select(job => new JobDetailResponse(
                job.Id,
                job.Slug,
                job.Title,
                job.Organization,
                job.Description,
                job.Category,
                job.EmploymentType,
                job.WorkMode,
                job.District == null ? null : job.District.Name,
                job.District == null ? null : job.District.Province.Name,
                job.ApplicationUrl,
                job.ApplicationDeadline,
                job.ApplicationDeadline <= today.AddDays(ClosingSoonWindowDays)
                    ? JobStatus.ClosingSoon
                    : JobStatus.Open,
                job.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Turns the province and district reference codes a caller sent into the ids the query needs.
    /// Codes rather than ids go over the wire because that is what the reference endpoints already
    /// publish, so the client never has to know a GUID, and a typo produces a specific
    /// <c>province_invalid</c> instead of silently matching nothing.
    ///
    /// A district that does not belong to the named province still filters to that district. The
    /// two are not cross-checked here: the filter is a narrowing, not a claim about the data, and
    /// the district is the more specific of the two. A posting cannot contradict its own location,
    /// because it only ever stores the district.
    /// </summary>
    internal async Task<JobLocationFilter> ResolveLocationFilterAsync(
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
                return new JobLocationFilter(null, null, "province");
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
                return new JobLocationFilter(provinceId, null, "district");
            }
        }

        return new JobLocationFilter(provinceId, districtId);
    }

    // ---------------------------------------------------------------- staff read

    /// <summary>
    /// The staff board. Unlike the public list this shows drafts and closed rows, because deciding
    /// what to retire is the whole point of the screen. The search, filters and paging are the same
    /// shape the reader uses, so there is one convention to learn.
    /// </summary>
    internal async Task<PagedResult<JobAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        JobCategory? category,
        EmploymentType? employmentType,
        WorkMode? workMode,
        JobAdminFilter filter,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var query = ApplyAdminFilters(
            dbContext.Jobs.AsNoTracking(),
            search,
            provinceId,
            districtId,
            category,
            employmentType,
            workMode,
            filter,
            today);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(job => job.UpdatedAt)
            .ThenBy(job => job.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(job => new JobAdminSummaryResponse(
                job.Id,
                job.Slug,
                job.Title,
                job.Organization,
                job.Category,
                job.EmploymentType,
                job.WorkMode,
                job.District == null ? null : job.District.Name,
                job.ApplicationDeadline,
                job.IsPublished,
                job.ApplicationDeadline < today,
                job.UpdatedAt,
                job.CreatedByUser == null ? null : job.CreatedByUser.Email))
            .ToListAsync(cancellationToken);

        return new PagedResult<JobAdminSummaryResponse>(items, paging.Page, paging.PageSize, totalCount);
    }

    internal async Task<JobAdminResponse?> FindForAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .Include(item => item.District)
                .ThenInclude(district => district!.Province)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var author = job.CreatedByUserId is null
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == job.CreatedByUserId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);

        return ToAdminResponse(job, author);
    }

    // ---------------------------------------------------------------- staff writes

    internal async Task<JobSaveOutcome> CreateAsync(
        SaveJobRequest request,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, dates.Today, cancellationToken);
        if (errors.Count > 0)
        {
            return new JobSaveOutcome(JobSaveStatus.Invalid, FieldErrors: errors);
        }

        if (await SlugExistsAsync(request.Slug, cancellationToken))
        {
            return new JobSaveOutcome(
                JobSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another posting.");
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
        };

        ApplyCommonFields(job, request, await ResolveDistrictAsync(request.DistrictCode, cancellationToken));

        dbContext.Jobs.Add(job);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index is the real guard. Two staff members saving the same slug at once is
            // an expected outcome, not a fault, and losing it is reported as the conflict it is
            // rather than as a server error.
            logger.LogInformation(exception, "A duplicate job slug was refused for {Slug}.", request.Slug);
            dbContext.Entry(job).State = EntityState.Detached;

            return new JobSaveOutcome(
                JobSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another posting.");
        }

        return new JobSaveOutcome(JobSaveStatus.Created, await FindForAdminAsync(job.Id, cancellationToken));
    }

    internal async Task<JobSaveOutcome> UpdateAsync(
        Guid id,
        SaveJobRequest request,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (job is null)
        {
            return new JobSaveOutcome(JobSaveStatus.NotFound);
        }

        // A published slug is the address the posting is live at, so it is frozen from that moment
        // on. A draft keeps its editable slug, because a staff member who mistyped it has not yet
        // sent anyone to the wrong URL and can still fix it.
        if (job.IsPublished
            && !string.Equals(job.Slug, request.Slug.Trim(), StringComparison.Ordinal))
        {
            return new JobSaveOutcome(
                JobSaveStatus.SlugImmutable,
                Message: "The slug cannot be changed once a posting is published, because that is the address people are already sharing. Unpublish it first if the address really has to change.");
        }

        var errors = await ValidateAsync(request, dates.Today, cancellationToken);
        if (errors.Count > 0)
        {
            return new JobSaveOutcome(JobSaveStatus.Invalid, FieldErrors: errors);
        }

        var trimmedSlug = request.Slug.Trim();
        if (!string.Equals(job.Slug, trimmedSlug, StringComparison.Ordinal)
            && await SlugExistsAsync(trimmedSlug, cancellationToken))
        {
            return new JobSaveOutcome(
                JobSaveStatus.SlugTaken,
                Message: $"The slug '{trimmedSlug}' is already used by another posting.");
        }

        var districtId = await ResolveDistrictAsync(request.DistrictCode, cancellationToken);

        ApplyCommonFields(job, request, districtId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new JobSaveOutcome(JobSaveStatus.Updated, await FindForAdminAsync(id, cancellationToken));
    }

    /// <summary>
    /// Removes a draft. A published row is refused, because the intended operation is to retract
    /// it and hide it from applicants, and deleting it instead loses the record of what was public
    /// and how long it was open. Nothing references a posting in this phase, so there is no third
    /// case.
    /// </summary>
    internal async Task<JobDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (job is null)
        {
            return JobDeleteStatus.NotFound;
        }

        if (job.IsPublished)
        {
            return JobDeleteStatus.Published;
        }

        dbContext.Jobs.Remove(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return JobDeleteStatus.Deleted;
    }

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// Strictly earlier than today is closed, so a deadline that falls today is still open: the
    /// day is not over, and an applicant applying on the deadline date is not too late.
    /// </summary>
    private IQueryable<Job> PublicScope(DateOnly today) =>
        dbContext.Jobs
            .AsNoTracking()
            .Where(job => job.IsPublished)
            .Where(job => job.ApplicationDeadline >= today);

    private static IQueryable<Job> ApplyFilters(
        IQueryable<Job> query,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        JobCategory? category,
        EmploymentType? employmentType,
        WorkMode? workMode)
    {
        if (districtId is { } selectedDistrict)
        {
            query = query.Where(job => job.DistrictId == selectedDistrict);
        }
        else if (provinceId is { } selectedProvince)
        {
            // A province filter is a filter on the district's province, not a column on the row.
            // There is no province_id on jobs precisely so the two cannot disagree, and this is
            // what that decision costs: one join in the filter, rather than two redundant columns.
            query = query.Where(job => job.District != null && job.District.ProvinceId == selectedProvince);
        }

        if (category is { } selectedCategory)
        {
            query = query.Where(job => job.Category == selectedCategory);
        }

        if (employmentType is { } selectedEmploymentType)
        {
            query = query.Where(job => job.EmploymentType == selectedEmploymentType);
        }

        if (workMode is { } selectedWorkMode)
        {
            query = query.Where(job => job.WorkMode == selectedWorkMode);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(job =>
                EF.Functions.Like(job.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(job.Organization, pattern, LikePattern.Escape)
                || EF.Functions.Like(job.Description, pattern, LikePattern.Escape));
        }

        return query;
    }

    private static IQueryable<Job> ApplyAdminFilters(
        IQueryable<Job> query,
        string? search,
        Guid? provinceId,
        Guid? districtId,
        JobCategory? category,
        EmploymentType? employmentType,
        WorkMode? workMode,
        JobAdminFilter filter,
        DateOnly today)
    {
        query = ApplyFilters(query, search, provinceId, districtId, category, employmentType, workMode);

        query = filter switch
        {
            JobAdminFilter.Published => query.Where(job => job.IsPublished),
            JobAdminFilter.Draft => query.Where(job => !job.IsPublished),
            JobAdminFilter.Expired => query.Where(job => job.ApplicationDeadline < today),
            _ => query,
        };

        return query;
    }

    /// <summary>
    /// The sort is chosen from a fixed set and applied as a real <c>OrderBy</c>, never
    /// concatenated into a string. The identifier ends every ordering so that two rows sharing a
    /// creation instant keep a stable order between pages, which is what stops a posting appearing
    /// on both page 1 and page 2.
    /// </summary>
    private static IQueryable<Job> ApplyPublicOrder(JobSort sort, IQueryable<Job> query) => sort switch
    {
        JobSort.Deadline => query
            .OrderBy(job => job.ApplicationDeadline)
            .ThenBy(job => job.Id),

        _ => query
            .OrderByDescending(job => job.CreatedAt)
            .ThenBy(job => job.Id),
    };

    private static void ApplyCommonFields(Job job, SaveJobRequest request, Guid? districtId)
    {
        job.Slug = request.Slug.Trim();
        job.Title = request.Title.Trim();
        job.Organization = request.Organization.Trim();
        job.Description = request.Description.Trim();
        job.Category = request.Category;
        job.EmploymentType = request.EmploymentType;
        job.WorkMode = request.WorkMode;
        job.DistrictId = districtId;
        job.ApplicationUrl = request.ApplicationUrl!.Trim();
        job.ApplicationDeadline = request.ApplicationDeadline;
        job.IsPublished = request.IsPublished;
    }

    private async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        await dbContext.Jobs
            .AnyAsync(job => job.Slug == slug.Trim(), cancellationToken);

    /// <summary>
    /// Resolves a district reference code to its id. An unknown code is refused rather than
    /// silently dropping the location, because a posting that says it is in Kathmandu and is
    /// actually nowhere in particular is exactly the wrong outcome for someone reading a board
    /// to find local work.
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
    /// Everything that must be true before a posting is worth showing an applicant, as field errors
    /// against the inputs the staff form actually has.
    ///
    /// This is the single source of the publish rules. The save path returns these as the reason a
    /// save was refused, and the staff editor returns the same list as
    /// <c>publishBlockers</c>, so what the editor tells you and what the server enforces cannot
    /// drift apart.
    ///
    /// A deadline in the past is refused, which means a staff member cannot publish a posting that
    /// has already closed. That is the intent: such a row is only ever created by the seeder,
    /// modelling a posting that was legitimately live and has since ended. Retracting is always
    /// allowed, and the row keeps its deadline either way.
    /// </summary>
    internal static IReadOnlyList<FieldError> CollectPublishBlockers(
        SaveJobRequest request,
        DateOnly today)
    {
        var errors = new List<FieldError>();

        if (request.Description.Trim().Length < MinPublishableDescription)
        {
            errors.Add(new FieldError(
                "description",
                "required",
                $"Write at least {MinPublishableDescription} characters before publishing. Someone will read this before they apply, and three lines tells them nothing about the work."));
        }

        if (request.ApplicationDeadline < today)
        {
            errors.Add(new FieldError(
                "applicationDeadline",
                "out_of_range",
                "That closing date has already passed, so this posting cannot be published. Use a future date, or keep it as a draft until the employer confirms the real one."));
        }

        return errors;
    }

    private async Task<IReadOnlyList<FieldError>> ValidateAsync(
        SaveJobRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();

        // The application link is required, unlike the resource module's optional one, because a
        // posting with no way to apply is a dead end for the reader this board exists to serve.
        if (string.IsNullOrWhiteSpace(request.ApplicationUrl))
        {
            errors.Add(new FieldError(
                ApplicationUrlField,
                "required",
                "A posting needs a link to where applicants should go, because NepalHub does not host applications."));
        }
        else if (!ExternalUrlPolicy.TryNormalise(request.ApplicationUrl, ApplicationUrlField, out _, out var urlError))
        {
            errors.Add(new FieldError(ApplicationUrlField, "invalid", urlError!));
        }

        if (!string.IsNullOrWhiteSpace(request.DistrictCode))
        {
            var districtId = await ResolveDistrictAsync(request.DistrictCode, cancellationToken);
            if (districtId is null)
            {
                errors.Add(new FieldError(
                    "districtCode",
                    "invalid",
                    $"'{request.DistrictCode.Trim()}' is not a district this site knows about. Choose one from the list."));
            }
        }

        if (request.IsPublished)
        {
            errors.AddRange(CollectPublishBlockers(request, today));
        }

        return errors;
    }

    private JobAdminResponse ToAdminResponse(Job job, string? author)
    {
        var today = dates.Today;

        return new JobAdminResponse(
            job.Id,
            job.Slug,
            job.Title,
            job.Organization,
            job.Description,
            job.Category,
            job.EmploymentType,
            job.WorkMode,
            job.District?.Code,
            job.District?.Name,
            job.District?.Province.Code,
            job.District?.Province.Name,
            job.ApplicationUrl,
            job.ApplicationDeadline,
            job.ApplicationDeadline < today,
            job.IsPublished,
            job.CreatedAt,
            job.UpdatedAt,
            author,
            job.IsPublished
                ? []
                : CollectPublishBlockers(ToSaveRequest(job), today).Select(error => error.Message).ToList());
    }

    /// <summary>
    /// Re-projects a stored row back into the request shape so the publish rules can be evaluated
    /// against it. Without this, the editor's blocker list would need a second copy of every rule,
    /// and the two copies would eventually disagree.
    /// </summary>
    private static SaveJobRequest ToSaveRequest(Job job) => new()
    {
        Slug = job.Slug,
        Title = job.Title,
        Organization = job.Organization,
        Description = job.Description,
        Category = job.Category,
        EmploymentType = job.EmploymentType,
        WorkMode = job.WorkMode,
        DistrictCode = job.District?.Code,
        ApplicationUrl = job.ApplicationUrl,
        ApplicationDeadline = job.ApplicationDeadline,
        IsPublished = job.IsPublished,
    };

    /// <summary>
    /// MySQL error 1062 is ER_DUP_ENTRY, matching the resource write path. Catching every
    /// <see cref="DbUpdateException"/> instead would report a dead connection or a truncated
    /// column as a friendly "that slug is taken".
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}
