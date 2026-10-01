using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Jobs;

/// <summary>
/// The public job board.
///
/// A class-level <c>[AllowAnonymous]</c> is safe here, and this is the reason it differs from the
/// quiz controller, which cannot have one: every action on this controller is a public read. There
/// is no action that depends on who is asking, because nothing in this phase is per-visitor and
/// saved postings are deferred. The attribute is collected across all endpoint metadata, so putting
/// it on a controller that later grows a signed-in action would quietly open that action up. If a
/// personal action is ever added here it must be a separate controller with the attribute removed.
/// </summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class JobsController(JobService jobService) : ControllerBase
{
    /// <summary>
    /// The board. Drafts and closed postings are excluded by the service, not by anything the
    /// caller can influence.
    ///
    /// The filter and sort arguments are read as strings and parsed here rather than bound as
    /// enums, so an unrecognised value produces a specific <c>category_invalid</c> or
    /// <c>sort_invalid</c> the UI can explain, instead of a framework binding message about a
    /// parameter. The location filters are reference codes rather than ids, which is what the
    /// reference endpoints publish and means the client never handles a GUID.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<JobSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<JobSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? category,
        string? employmentType,
        string? workMode,
        string? sort,
        CancellationToken cancellationToken)
    {
        if (!JobFilters.TryParseEnum<JobCategory>(category, nameof(category), out var selectedCategory, out var categoryField))
        {
            return BadFilter(categoryField);
        }

        if (!JobFilters.TryParseEnum<EmploymentType>(employmentType, nameof(employmentType), out var selectedEmploymentType, out var employmentTypeField))
        {
            return BadFilter(employmentTypeField);
        }

        if (!JobFilters.TryParseEnum<WorkMode>(workMode, nameof(workMode), out var selectedWorkMode, out var workModeField))
        {
            return BadFilter(workModeField);
        }

        if (!JobFilters.TryParseEnum<JobSort>(sort, nameof(sort), out var parsedSort, out var sortField))
        {
            return BadFilter(sortField);
        }

        var location = await jobService.ResolveLocationFilterAsync(province, district, cancellationToken);
        if (location.InvalidField is { } invalidField)
        {
            return BadFilter(invalidField);
        }

        var query = new JobListQuery(
            PageRequest.Normalise(page, pageSize),
            search,
            location.ProvinceId,
            location.DistrictId,
            selectedCategory,
            selectedEmploymentType,
            selectedWorkMode,
            parsedSort ?? JobSort.Newest);

        var result = await jobService.ListAsync(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<JobSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// One posting. A draft, a closed posting and a slug that never existed all return the same
    /// 404, so this endpoint cannot be used to discover which slugs are real but not yet public.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ApiResponse<JobDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JobDetailResponse>>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var job = await jobService.FindBySlugAsync(slug, cancellationToken);

        if (job is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("job_not_found", "That job is not available. It may have closed, or the link may be out of date.")));
        }

        return Ok(ApiResponse<JobDetailResponse>.Ok(job));
    }

    private BadRequestObjectResult BadFilter(string field)
    {
        IReadOnlyList<FieldError> details =
        [
            new FieldError(field, "invalid", "Choose one of the offered values."),
        ];

        return BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a value this job board understands.",
            details)));
    }
}
