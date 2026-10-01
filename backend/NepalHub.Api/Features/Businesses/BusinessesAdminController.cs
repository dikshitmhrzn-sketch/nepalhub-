using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Businesses;

/// <summary>
/// Staff management of the business directory. It sits in this module's folder beside the public
/// controller so the slice stays in one place, exactly as the quiz, resource and job admin
/// controllers do.
///
/// Every action here is behind the staff policy, and the middleware chain already requires a CSRF
/// token for the unsafe ones, so neither concern is handled a second time in this file.
/// </summary>
[ApiController]
[Route("api/admin/businesses")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class BusinessesAdminController(
    BusinessService businessService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The staff directory: drafts included, plus a status filter a public reader has no equivalent
    /// of.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<BusinessAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<BusinessAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? category,
        string? verified,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!BusinessFilters.TryParseEnum<BusinessCategory>(category, nameof(category), out var selectedCategory, out var categoryField))
        {
            return BadFilter(categoryField);
        }

        if (!BusinessFilters.TryParseEnum<BusinessAdminFilter>(status, nameof(status), out var selectedStatus, out var statusField))
        {
            return BadFilter(statusField);
        }

        if (!BusinessFilters.TryParseBool(verified, nameof(verified), out var parsedVerified, out var verifiedField))
        {
            return BadFilter(verifiedField);
        }

        var location = await businessService.ResolveLocationFilterAsync(province, district, cancellationToken);
        if (location.InvalidField is { } invalidField)
        {
            return BadFilter(invalidField);
        }

        var result = await businessService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            search,
            location.ProvinceId,
            location.DistrictId,
            selectedCategory,
            parsedVerified,
            selectedStatus ?? BusinessAdminFilter.All,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<BusinessAdminSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// The full record for the editor. It carries the reasons a draft cannot be published yet, so the
    /// form can show them before staff try to save and are refused.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<BusinessAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BusinessAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var business = await businessService.FindForAdminAsync(id, cancellationToken);

        if (business is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("business_not_found", $"No listing with id '{id}'.")));
        }

        return Ok(ApiResponse<BusinessAdminResponse>.Ok(business));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<BusinessAdminResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<BusinessAdminResponse>>> Create(
        SaveBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await businessService.CreateAsync(request, author?.Id, cancellationToken);

        return outcome.Status switch
        {
            BusinessSaveStatus.Created => Created(
                $"/api/admin/businesses/{outcome.Business!.Id}",
                ApiResponse<BusinessAdminResponse>.Ok(outcome.Business)),

            BusinessSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another listing."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The listing could not be saved.", outcome.FieldErrors))),
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<BusinessAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<BusinessAdminResponse>>> Update(
        Guid id,
        SaveBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await businessService.UpdateAsync(id, request, cancellationToken);

        return outcome.Status switch
        {
            BusinessSaveStatus.Updated => Ok(ApiResponse<BusinessAdminResponse>.Ok(outcome.Business!)),

            BusinessSaveStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("business_not_found", $"No listing with id '{id}'."))),

            // A conflict rather than a validation failure: the request was well formed, and the state
            // of the row simply no longer allows this particular change.
            BusinessSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another listing."))),

            BusinessSaveStatus.SlugImmutable => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_immutable", outcome.Message ?? "The slug cannot be changed once a listing is published."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The listing could not be saved.", outcome.FieldErrors))),
        };
    }

    /// <summary>
    /// Removes a draft, and its opening hours with it. A published row is refused: the intended
    /// operation is to retract it, so that the record of what was public is not simply lost.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var status = await businessService.DeleteAsync(id, cancellationToken);

        return status switch
        {
            BusinessDeleteStatus.Deleted => NoContent(),

            BusinessDeleteStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("business_not_found", $"No listing with id '{id}'."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "business_published",
                "This listing is published, so it cannot be deleted. Unpublish it instead."))),
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
            $"'{field}' is not a value this directory understands.",
            details)));
    }
}