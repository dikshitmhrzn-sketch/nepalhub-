using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Jobs;

/// <summary>
/// Staff management of the job board. It sits in this module's folder beside the public controller
/// so the slice stays in one place, exactly as the quiz and resource admin controllers do.
///
/// Every action here is behind the staff policy, and the middleware chain already requires a CSRF
/// token for the unsafe ones, so neither concern is handled a second time in this file.
/// </summary>
[ApiController]
[Route("api/admin/jobs")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class JobsAdminController(
    JobService jobService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The staff board: drafts and closed postings included, plus a status filter that a public
    /// reader has no equivalent of.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<JobAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<JobAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? category,
        string? employmentType,
        string? workMode,
        string? status,
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

        if (!JobFilters.TryParseEnum<JobAdminFilter>(status, nameof(status), out var selectedStatus, out var statusField))
        {
            return BadFilter(statusField);
        }

        var location = await jobService.ResolveLocationFilterAsync(province, district, cancellationToken);
        if (location.InvalidField is { } invalidField)
        {
            return BadFilter(invalidField);
        }

        var paging = PageRequest.Normalise(page, pageSize);

        var result = await jobService.ListForAdminAsync(
            paging,
            search,
            location.ProvinceId,
            location.DistrictId,
            selectedCategory,
            selectedEmploymentType,
            selectedWorkMode,
            selectedStatus ?? JobAdminFilter.All,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<JobAdminSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// The full record for the editor. It carries the reasons a draft cannot be published yet, so
    /// the form can show them before staff try to save and are refused.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<JobAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JobAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job = await jobService.FindForAdminAsync(id, cancellationToken);

        if (job is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("job_not_found", $"No job with id '{id}'.")));
        }

        return Ok(ApiResponse<JobAdminResponse>.Ok(job));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<JobAdminResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<JobAdminResponse>>> Create(
        SaveJobRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await jobService.CreateAsync(request, author?.Id, cancellationToken);

        return outcome.Status switch
        {
            JobSaveStatus.Created => Created(
                $"/api/admin/jobs/{outcome.Job!.Id}",
                ApiResponse<JobAdminResponse>.Ok(outcome.Job)),

            JobSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another posting."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The job posting could not be saved.", outcome.FieldErrors))),
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<JobAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<JobAdminResponse>>> Update(
        Guid id,
        SaveJobRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await jobService.UpdateAsync(id, request, cancellationToken);

        return outcome.Status switch
        {
            JobSaveStatus.Updated => Ok(ApiResponse<JobAdminResponse>.Ok(outcome.Job!)),

            JobSaveStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("job_not_found", $"No job with id '{id}'."))),

            // A conflict rather than a validation failure: the request was well formed, and the
            // state of the row simply no longer allows this particular change.
            JobSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another posting."))),

            JobSaveStatus.SlugImmutable => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_immutable", outcome.Message ?? "The slug cannot be changed once a posting is published."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The job posting could not be saved.", outcome.FieldErrors))),
        };
    }

    /// <summary>
    /// Removes a draft. A published row is refused: the intended operation is to retract it, so
    /// that the record of what was public is not simply lost.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var status = await jobService.DeleteAsync(id, cancellationToken);

        return status switch
        {
            JobDeleteStatus.Deleted => NoContent(),

            JobDeleteStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("job_not_found", $"No job with id '{id}'."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "job_published",
                "This posting is published, so it cannot be deleted. Unpublish it instead."))),
        };
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
