using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Quiz;

namespace NepalHub.Api.Features.Students;

public enum ResourceSaveStatus
{
    Created,
    Updated,
    NotFound,

    /// <summary>Shape or content rules were not met.</summary>
    Invalid,

    /// <summary>Another row already owns that slug.</summary>
    SlugTaken,

    /// <summary>The slug is the public URL, so it is fixed once the row exists.</summary>
    SlugImmutable,
}

public enum ResourceDeleteStatus
{
    Deleted,
    NotFound,
    Published,
}

/// <summary>Which slice of the catalogue the staff list is showing.</summary>
public enum ResourceAdminFilter
{
    All = 0,
    Published = 1,
    Draft = 2,

    /// <summary>Published or not, but past its deadline. The only filter a public reader has no equivalent of.</summary>
    Expired = 3,
}

/// <summary>The validated, already-normalised form of a catalogue request.</summary>
public sealed record ResourceListQuery(
    PageRequest Paging,
    string? Search,
    ResourceType? Type,
    ResourceCategory? Category,
    EducationLevel? Level,
    ResourceSort Sort);

public sealed record ResourceSaveOutcome(
    ResourceSaveStatus Status,
    ResourceAdminResponse? Resource = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

/// <summary>
/// Every rule for the student resource catalogue: browsing it as a reader and managing it as
/// staff. The controllers only map outcomes onto HTTP, as the quiz and calculator services do.
///
/// The one rule that shapes most of the code is that published and unexpired are separate
/// concerns but always travel together on the public path. A draft is invisible because nobody
/// has approved it; an expired scholarship is invisible because it is now worse than useless to
/// a student who acts on it. Both produce the same answer to a reader, and the same 404, so
/// neither can be probed.
/// </summary>
public sealed class StudentResourceService(
    AppDbContext dbContext,
    INepalDateProvider dates,
    ILogger<StudentResourceService> logger)
{
    /// <summary>
    /// A body shorter than this on a published resource is almost always a placeholder that was
    /// never finished, and a catalogue of stubs is worse than a smaller catalogue.
    /// </summary>
    private const int MinPublishableSummary = 20;

    private const int MinPublishableBody = 80;

    private const int MinAcademicYear = 2000;
    private const int MaxAcademicYear = 2100;

    // ---------------------------------------------------------------- public read

    /// <summary>
    /// The catalogue. The published and not-expired predicates are applied here and cannot be
    /// switched off from the outside, which is deliberate: an <c>includeUnpublished</c> flag on a
    /// public endpoint is a way to leak drafts by accident, and a flag nobody can turn on by
    /// accident is not a flag.
    /// </summary>
    internal async Task<PagedResult<ResourceSummaryResponse>> ListAsync(
        ResourceListQuery request,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var query = ApplyFilters(
            PublicScope(today),
            request.Search,
            request.Type,
            request.Category,
            request.Level);

        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = ApplyPublicOrder(request.Sort, query);

        var items = await ordered
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .Select(resource => new ResourceSummaryResponse(
                resource.Id,
                resource.Slug,
                resource.Type,
                resource.Category,
                resource.Level,
                resource.Title,
                resource.Summary,
                resource.ExternalUrl != null,
                resource.ExpiresOn,
                resource.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ResourceSummaryResponse>(items, request.Paging.Page, request.Paging.PageSize, totalCount);
    }

    /// <summary>
    /// One resource as a reader sees it. Returns null for a draft, an expired row, a
    /// discontinued row and a slug that never existed, all identically, so this endpoint cannot
    /// be used to find out which slugs are real but not yet public.
    /// </summary>
    internal async Task<ResourceDetailResponse?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var today = dates.Today;

        return await PublicScope(today)
            .Where(resource => resource.Slug == slug)
            .Select(resource => new ResourceDetailResponse(
                resource.Id,
                resource.Slug,
                resource.Type,
                resource.Category,
                resource.Level,
                resource.Title,
                resource.Summary,
                resource.Body,
                resource.ExternalUrl,
                resource.AcademicYear,
                resource.Provider,
                resource.AwardAmountNpr,
                resource.Eligibility,
                resource.ExpiresOn,
                resource.ExpiresOn != null && resource.ExpiresOn < today,
                resource.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- staff read

    /// <summary>
    /// The staff catalogue. Unlike the public list this shows drafts and expired rows, because
    /// deciding what to retire is the whole point of the screen. The search, filters and paging
    /// are the same shape the reader uses, so there is one convention to learn.
    /// </summary>
    internal async Task<PagedResult<ResourceAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        string? search,
        ResourceType? type,
        ResourceCategory? category,
        EducationLevel? level,
        ResourceAdminFilter filter,
        CancellationToken cancellationToken)
    {
        var today = dates.Today;
        var query = dbContext.StudentResources.AsNoTracking();

        if (type is { } selectedType)
        {
            query = query.Where(resource => resource.Type == selectedType);
        }

        if (category is { } selectedCategory)
        {
            query = query.Where(resource => resource.Category == selectedCategory);
        }

        if (level is { } selectedLevel)
        {
            query = query.Where(resource => resource.Level == selectedLevel);
        }

        query = filter switch
        {
            ResourceAdminFilter.Published => query.Where(resource => resource.IsPublished),
            ResourceAdminFilter.Draft => query.Where(resource => !resource.IsPublished),
            // Strictly earlier than today, so a deadline that falls today is still open: the day
            // is not over, and a student applying on the deadline date is not too late.
            ResourceAdminFilter.Expired => query.Where(resource => resource.ExpiresOn != null && resource.ExpiresOn < today),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(resource =>
                EF.Functions.Like(resource.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(resource.Summary, pattern, LikePattern.Escape));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(resource => resource.UpdatedAt)
            .ThenBy(resource => resource.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(resource => new ResourceAdminSummaryResponse(
                resource.Id,
                resource.Slug,
                resource.Type,
                resource.Category,
                resource.Level,
                resource.Title,
                resource.IsPublished,
                resource.ExpiresOn != null && resource.ExpiresOn < today,
                resource.ExpiresOn,
                resource.UpdatedAt,
                resource.CreatedByUser == null ? null : resource.CreatedByUser.Email))
            .ToListAsync(cancellationToken);

        return new PagedResult<ResourceAdminSummaryResponse>(items, paging.Page, paging.PageSize, totalCount);
    }

    internal async Task<ResourceAdminResponse?> FindForAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var resource = await dbContext.StudentResources
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (resource is null)
        {
            return null;
        }

        var author = resource.CreatedByUserId is null
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == resource.CreatedByUserId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);

        return ToAdminResponse(resource, author);
    }

    // ---------------------------------------------------------------- staff writes

    internal async Task<ResourceSaveOutcome> CreateAsync(
        SaveResourceRequest request,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        if (!ExternalUrlPolicy.TryNormalise(request.ExternalUrl, out var url, out var urlError))
        {
            return Invalid(ExternalUrlPolicy.Field, "invalid", urlError!);
        }

        if (await SlugExistsAsync(request.Slug, cancellationToken))
        {
            return new ResourceSaveOutcome(
                ResourceSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another resource.");
        }

        if (request.IsPublished)
        {
            var blockers = CollectPublishBlockers(request, url, dates.Today);
            if (blockers.Count > 0)
            {
                return new ResourceSaveOutcome(ResourceSaveStatus.Invalid, FieldErrors: blockers);
            }
        }

        var resource = new StudentResource
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug.Trim(),
            CreatedByUserId = createdByUserId,
        };

        ApplyCommonFields(resource, request, url);

        dbContext.StudentResources.Add(resource);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index is the real guard. Two staff members saving the same slug at once
            // is an expected outcome, not a fault, and losing it is reported as the conflict it
            // is rather than as a server error.
            logger.LogInformation(exception, "A duplicate resource slug was refused for {Slug}.", request.Slug);
            dbContext.Entry(resource).State = EntityState.Detached;

            return new ResourceSaveOutcome(
                ResourceSaveStatus.SlugTaken,
                Message: $"The slug '{request.Slug}' is already used by another resource.");
        }

        return new ResourceSaveOutcome(ResourceSaveStatus.Created, await FindForAdminAsync(resource.Id, cancellationToken));
    }

    internal async Task<ResourceSaveOutcome> UpdateAsync(
        Guid id,
        SaveResourceRequest request,
        CancellationToken cancellationToken)
    {
        var resource = await dbContext.StudentResources
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (resource is null)
        {
            return new ResourceSaveOutcome(ResourceSaveStatus.NotFound);
        }

        // The slug is the public URL, so it is fixed for the life of the row. A changing URL
        // breaks every bookmark, shared link and search result already pointing at it, and
        // redirecting is a feature this phase does not have.
        if (!string.Equals(resource.Slug, request.Slug.Trim(), StringComparison.Ordinal))
        {
            return new ResourceSaveOutcome(
                ResourceSaveStatus.SlugImmutable,
                Message: "The slug cannot be changed, because it is the address this resource is published at.");
        }

        if (!ExternalUrlPolicy.TryNormalise(request.ExternalUrl, out var url, out var urlError))
        {
            return Invalid(ExternalUrlPolicy.Field, "invalid", urlError!);
        }

        if (request.IsPublished)
        {
            var blockers = CollectPublishBlockers(request, url, dates.Today);
            if (blockers.Count > 0)
            {
                return new ResourceSaveOutcome(ResourceSaveStatus.Invalid, FieldErrors: blockers);
            }
        }

        resource.Title = request.Title.Trim();
        resource.Summary = request.Summary.Trim();
        resource.Body = request.Body.Trim();
        resource.Type = request.Type;
        resource.Category = request.Category;
        resource.Level = request.Level;
        resource.IsPublished = request.IsPublished;

        // Cleared rather than stored, so a field that does not apply to the row's type cannot
        // sit in the database looking meaningful. A note cannot have an academic year, and a
        // question paper cannot have a scholarship provider.
        resource.ExternalUrl = url;
        resource.AcademicYear = request.Type == ResourceType.QuestionPaper ? request.AcademicYear : null;
        resource.Provider = request.Type == ResourceType.Scholarship ? request.Provider?.Trim() : null;
        resource.AwardAmountNpr = request.Type == ResourceType.Scholarship ? request.AwardAmountNpr : null;
        resource.Eligibility = request.Type == ResourceType.Scholarship ? request.Eligibility?.Trim() : null;

        // A row that stops being a scholarship should not keep a deadline it no longer needs,
        // or the staff list would keep showing it as expiring.
        resource.ExpiresOn = request.ExpiresOn;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResourceSaveOutcome(ResourceSaveStatus.Updated, await FindForAdminAsync(id, cancellationToken));
    }

    /// <summary>
    /// Removes a draft. A published row is refused, because the intended operation is to retract
    /// it and hide it from readers, and deleting it instead loses the record of what was
    /// published. Nothing references a resource in this phase, so there is no third case.
    /// </summary>
    internal async Task<ResourceDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var resource = await dbContext.StudentResources
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (resource is null)
        {
            return ResourceDeleteStatus.NotFound;
        }

        if (resource.IsPublished)
        {
            return ResourceDeleteStatus.Published;
        }

        dbContext.StudentResources.Remove(resource);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ResourceDeleteStatus.Deleted;
    }

    // ------------------------------------------------------------------ internals

    private IQueryable<StudentResource> PublicScope(DateOnly today) =>
        dbContext.StudentResources
            .AsNoTracking()
            .Where(resource => resource.IsPublished)
            .Where(resource => resource.ExpiresOn == null || resource.ExpiresOn >= today);

    private static IQueryable<StudentResource> ApplyFilters(
        IQueryable<StudentResource> query,
        string? search,
        ResourceType? type,
        ResourceCategory? category,
        EducationLevel? level)
    {
        if (type is { } selectedType)
        {
            query = query.Where(resource => resource.Type == selectedType);
        }

        if (category is { } selectedCategory)
        {
            query = query.Where(resource => resource.Category == selectedCategory);
        }

        if (level is { } selectedLevel)
        {
            query = query.Where(resource => resource.Level == selectedLevel);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(resource =>
                EF.Functions.Like(resource.Title, pattern, LikePattern.Escape)
                || EF.Functions.Like(resource.Summary, pattern, LikePattern.Escape));
        }

        return query;
    }

    /// <summary>
    /// The sort is chosen from a fixed set and applied as a real <c>OrderBy</c>, never
    /// concatenated into a string. The identifier ends every ordering so that two rows with the
    /// same title or the same creation instant keep a stable order between pages, which is what
    /// stops a row appearing on both page 1 and page 2.
    /// </summary>
    private static IQueryable<StudentResource> ApplyPublicOrder(
        ResourceSort sort,
        IQueryable<StudentResource> query) => sort switch
        {
            ResourceSort.Title => query
                .OrderBy(resource => resource.Title)
                .ThenBy(resource => resource.Id),

            // MySQL sorts NULL first on an ascending order, which would push every note and
            // every paper with no deadline to the top of a list meant to show the deadlines
            // closing soonest. The boolean sort puts "has a deadline" ahead of "has none", and
            // then the date itself orders the group.
            ResourceSort.Expiring => query
                .OrderBy(resource => resource.ExpiresOn == null)
                .ThenBy(resource => resource.ExpiresOn)
                .ThenBy(resource => resource.Id),

            _ => query
                .OrderByDescending(resource => resource.CreatedAt)
                .ThenBy(resource => resource.Id),
        };

    private static void ApplyCommonFields(
        StudentResource resource,
        SaveResourceRequest request,
        string? url)
    {
        resource.Title = request.Title.Trim();
        resource.Summary = request.Summary.Trim();
        resource.Body = request.Body.Trim();
        resource.Type = request.Type;
        resource.Category = request.Category;
        resource.Level = request.Level;
        resource.IsPublished = request.IsPublished;
        resource.ExternalUrl = url;
        resource.AcademicYear = request.Type == ResourceType.QuestionPaper ? request.AcademicYear : null;
        resource.Provider = request.Type == ResourceType.Scholarship ? request.Provider?.Trim() : null;
        resource.AwardAmountNpr = request.Type == ResourceType.Scholarship ? request.AwardAmountNpr : null;
        resource.Eligibility = request.Type == ResourceType.Scholarship ? request.Eligibility?.Trim() : null;
        resource.ExpiresOn = request.ExpiresOn;
    }

    private async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        await dbContext.StudentResources
            .AnyAsync(resource => resource.Slug == slug.Trim(), cancellationToken);

    /// <summary>
    /// Everything that must be true before a resource is worth showing a reader, as field errors
    /// against the inputs the staff form actually has.
    ///
    /// This is the single source of the publish rules. The save path returns these as the reason
    /// a save was refused, and the staff editor returns the same list as
    /// <c>publishBlockers</c>, so what the editor tells you and what the server enforces cannot
    /// drift apart.
    ///
    /// A deadline in the past is refused for a scholarship, which means a staff member cannot
    /// publish a scholarship that has already closed. That is the intent: such a row is only ever
    /// created by the seeder, modelling a scholarship that was legitimately live and has since
    /// ended. Retracting is always allowed, and the row keeps its deadline either way.
    /// </summary>
    internal static IReadOnlyList<FieldError> CollectPublishBlockers(
        SaveResourceRequest request,
        string? normalisedUrl,
        DateOnly today)
    {
        var errors = new List<FieldError>();

        if (request.Summary.Trim().Length < MinPublishableSummary)
        {
            errors.Add(new FieldError(
                "summary",
                "required",
                $"Write a summary of at least {MinPublishableSummary} characters before publishing, so a reader can tell what this is from the catalogue card."));
        }

        if (request.Body.Trim().Length < MinPublishableBody)
        {
            errors.Add(new FieldError(
                "body",
                "required",
                $"The content must be at least {MinPublishableBody} characters before publishing. A published resource needs real content, not a placeholder."));
        }

        switch (request.Type)
        {
            case ResourceType.QuestionPaper:
                if (request.AcademicYear is not { } year || year < MinAcademicYear || year > MaxAcademicYear)
                {
                    errors.Add(new FieldError(
                        "academicYear",
                        "required",
                        $"A question paper needs the year it belongs to, between {MinAcademicYear} and {MaxAcademicYear}."));
                }

                if (string.IsNullOrWhiteSpace(normalisedUrl))
                {
                    errors.Add(new FieldError(
                        ExternalUrlPolicy.Field,
                        "required",
                        "A question paper needs a link to where the paper can be read, because NepalHub does not host files."));
                }

                break;

            case ResourceType.Scholarship:
                if (string.IsNullOrWhiteSpace(request.Provider))
                {
                    errors.Add(new FieldError(
                        "provider",
                        "required",
                        "A scholarship needs to say who offers it."));
                }

                if (request.ExpiresOn is not { } deadline)
                {
                    errors.Add(new FieldError(
                        "expiresOn",
                        "required",
                        "A scholarship needs a closing date, because that is what hides it once applications have closed."));
                }
                else if (deadline < today)
                {
                    errors.Add(new FieldError(
                        "expiresOn",
                        "out_of_range",
                        "That closing date has already passed, so this scholarship cannot be published. Use a future date, or keep it as a draft."));
                }

                break;
        }

        return errors;
    }

    private ResourceAdminResponse ToAdminResponse(StudentResource resource, string? author)
    {
        var today = dates.Today;

        return new ResourceAdminResponse(
            resource.Id,
            resource.Slug,
            resource.Type,
            resource.Category,
            resource.Level,
            resource.Title,
            resource.Summary,
            resource.Body,
            resource.ExternalUrl,
            resource.AcademicYear,
            resource.Provider,
            resource.AwardAmountNpr,
            resource.Eligibility,
            resource.ExpiresOn,
            resource.ExpiresOn is { } deadline && deadline < today,
            resource.IsPublished,
            resource.CreatedAt,
            resource.UpdatedAt,
            author,
            resource.IsPublished
                ? []
                : ToBlockerMessages(CollectPublishBlockers(ToSaveRequest(resource), resource.ExternalUrl, today)));
    }

    /// <summary>
    /// Re-projects a stored row back into the request shape so the publish rules can be evaluated
    /// against it. Without this, the editor's blocker list would need a second copy of every
    /// rule, and the two copies would eventually disagree.
    /// </summary>
    private static SaveResourceRequest ToSaveRequest(StudentResource resource) => new()
    {
        Slug = resource.Slug,
        Title = resource.Title,
        Summary = resource.Summary,
        Body = resource.Body,
        Type = resource.Type,
        Category = resource.Category,
        Level = resource.Level,
        ExternalUrl = resource.ExternalUrl,
        AcademicYear = resource.AcademicYear,
        Provider = resource.Provider,
        AwardAmountNpr = resource.AwardAmountNpr,
        Eligibility = resource.Eligibility,
        ExpiresOn = resource.ExpiresOn,
        IsPublished = resource.IsPublished,
    };

    private static IReadOnlyList<string> ToBlockerMessages(IReadOnlyList<FieldError> errors) =>
        errors.Select(error => error.Message).ToList();

    private static ResourceSaveOutcome Invalid(string field, string code, string message) =>
        new(ResourceSaveStatus.Invalid, FieldErrors: [new FieldError(field, code, message)]);

    /// <summary>
    /// MySQL error 1062 is ER_DUP_ENTRY, matching the quiz write paths. Catching every
    /// <see cref="DbUpdateException"/> instead would report a dead connection or a truncated
    /// column as a friendly "that slug is taken".
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}
