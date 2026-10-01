using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Students;

/// <summary>
/// Staff management of the resource catalogue. It sits in this module's folder beside the public
/// controller so the slice stays in one place, exactly as the quiz admin does.
///
/// Every action here is behind the staff policy, and the middleware chain already requires a
/// CSRF token for the unsafe ones, so neither concern is handled a second time in this file.
/// </summary>
[ApiController]
[Route("api/admin/students/resources")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class StudentResourcesAdminController(
    StudentResourceService resourceService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The staff catalogue: drafts and expired rows included, plus a status filter that a public
    /// reader has no equivalent of.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<ResourceAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<ResourceAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? type,
        string? category,
        string? level,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!TryParseFilter<ResourceType>(type, nameof(type), out var selectedType, out var typeError))
        {
            return BadFilter(typeError);
        }

        if (!TryParseFilter<ResourceCategory>(category, nameof(category), out var selectedCategory, out var categoryError))
        {
            return BadFilter(categoryError);
        }

        if (!TryParseFilter<EducationLevel>(level, nameof(level), out var selectedLevel, out var levelError))
        {
            return BadFilter(levelError);
        }

        if (!TryParseFilter<ResourceAdminFilter>(status, nameof(status), out var selectedStatus, out var statusError))
        {
            return BadFilter(statusError);
        }

        var paging = PageRequest.Normalise(page, pageSize);

        var result = await resourceService.ListForAdminAsync(
            paging,
            search,
            selectedType,
            selectedCategory,
            selectedLevel,
            selectedStatus ?? ResourceAdminFilter.All,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<ResourceAdminSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// The full record for the editor. It carries the reasons a draft cannot be published yet, so
    /// the form can show them before staff try to save and are refused.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<ResourceAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResourceAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resource = await resourceService.FindForAdminAsync(id, cancellationToken);

        if (resource is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("resource_not_found", $"No resource with id '{id}'.")));
        }

        return Ok(ApiResponse<ResourceAdminResponse>.Ok(resource));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<ResourceAdminResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ResourceAdminResponse>>> Create(
        SaveResourceRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await resourceService.CreateAsync(request, author?.Id, cancellationToken);

        return outcome.Status switch
        {
            ResourceSaveStatus.Created => Created(
                $"/api/admin/students/resources/{outcome.Resource!.Id}",
                ApiResponse<ResourceAdminResponse>.Ok(outcome.Resource)),

            ResourceSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another resource."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The resource could not be saved.", outcome.FieldErrors))),
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<ResourceAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ResourceAdminResponse>>> Update(
        Guid id,
        SaveResourceRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await resourceService.UpdateAsync(id, request, cancellationToken);

        return outcome.Status switch
        {
            ResourceSaveStatus.Updated => Ok(ApiResponse<ResourceAdminResponse>.Ok(outcome.Resource!)),

            ResourceSaveStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("resource_not_found", $"No resource with id '{id}'."))),

            // A conflict rather than a validation failure: the request was well formed, and the
            // state of the row simply no longer allows this particular change.
            ResourceSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another resource."))),

            ResourceSaveStatus.SlugImmutable => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_immutable", outcome.Message ?? "The slug cannot be changed once it exists."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The resource could not be saved.", outcome.FieldErrors))),
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
        var status = await resourceService.DeleteAsync(id, cancellationToken);

        return status switch
        {
            ResourceDeleteStatus.Deleted => NoContent(),

            ResourceDeleteStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("resource_not_found", $"No resource with id '{id}'."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "resource_published",
                "This resource is published, so it cannot be deleted. Unpublish it instead."))),
        };
    }

    private static bool TryParseFilter<T>(string? raw, string name, out T? value, out string field)
        where T : struct, Enum
    {
        field = name;

        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;

            return true;
        }

        if (Enum.TryParse<T>(raw.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;

            return true;
        }

        value = null;

        return false;
    }

    private BadRequestObjectResult BadFilter(string field)
    {
        IReadOnlyList<FieldError> details =
        [
            new FieldError(field, "invalid", "Choose one of the offered values."),
        ];

        return BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a value this catalogue understands.",
            details)));
    }
}
