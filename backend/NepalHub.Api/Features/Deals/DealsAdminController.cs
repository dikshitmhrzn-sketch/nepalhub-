using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Deals;

/// <summary>
/// Staff management of the offers page. It sits in this module's folder beside the public controller so
/// the slice stays in one place, exactly as the quiz, resource, job and business admin controllers do.
///
/// Every action here is behind the staff policy, and the middleware chain already requires a CSRF token
/// for the unsafe ones, so neither concern is handled a second time in this file.
/// </summary>
[ApiController]
[Route("api/admin/deals")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class DealsAdminController(
    DealService dealService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The staff offers list: drafts included, plus a status filter a public reader has no equivalent
    /// of. Expired offers stay in the list with a flag rather than disappearing, because the question a
    /// staff member brings to this screen is usually why something is not showing.
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<DealAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<DealAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!BusinessFilters.TryParseEnum<DealAdminFilter>(status, nameof(status), out var selectedStatus, out var statusField))
        {
            return BadFilter(statusField);
        }

        var location = await dealService.ResolveLocationFilterAsync(province, district, cancellationToken);
        if (location.InvalidField is { } invalidField)
        {
            return BadFilter(invalidField);
        }

        var result = await dealService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            search,
            location.ProvinceId,
            location.DistrictId,
            selectedStatus ?? DealAdminFilter.All,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<DealAdminSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// The published businesses an offer can be attached to, for the editor's picker. Separate from the
    /// offers list because it is a different question with a different answer shape, and it must not be
    /// able to return an unpublished listing: there is nothing to attach to.
    [HttpGet("businesses")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DealBusinessOptionResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DealBusinessOptionResponse>>>> ListBusinesses(
        string? search,
        CancellationToken cancellationToken)
    {
        var options = await dealService.ListBusinessOptionsAsync(search, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<DealBusinessOptionResponse>>.Ok(options));
    }

    /// <summary>
    /// The full record for the editor. It carries the reasons a draft cannot be published yet, so the
    /// form can show them before staff try to save and are refused.
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<DealAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DealAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deal = await dealService.FindForAdminAsync(id, cancellationToken);

        if (deal is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("deal_not_found", $"No offer with id '{id}'.")));
        }

        return Ok(ApiResponse<DealAdminResponse>.Ok(deal));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<DealAdminResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DealAdminResponse>>> Create(
        SaveDealRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await dealService.CreateAsync(request, author?.Id, cancellationToken);

        return outcome.Status switch
        {
            DealSaveStatus.Created => Created(
                $"/api/admin/deals/{outcome.Deal!.Id}",
                ApiResponse<DealAdminResponse>.Ok(outcome.Deal)),

            DealSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another offer."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The offer could not be saved.", outcome.FieldErrors))),
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<DealAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DealAdminResponse>>> Update(
        Guid id,
        SaveDealRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await dealService.UpdateAsync(id, request, cancellationToken);

        return outcome.Status switch
        {
            DealSaveStatus.Updated => Ok(ApiResponse<DealAdminResponse>.Ok(outcome.Deal!)),

            DealSaveStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("deal_not_found", $"No offer with id '{id}'."))),

            // A conflict rather than a validation failure: the request was well formed, and the state of
            // the row simply no longer allows this particular change.
            DealSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another offer."))),

            DealSaveStatus.SlugImmutable => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_immutable", outcome.Message ?? "The slug cannot be changed once an offer is published."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The offer could not be saved.", outcome.FieldErrors))),
        };
    }

    /// <summary>
    /// Removes a draft. A published row is refused: the intended operation is to retract it, so that the
    /// record of what readers were shown is not simply lost.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var status = await dealService.DeleteAsync(id, cancellationToken);

        return status switch
        {
            DealDeleteStatus.Deleted => NoContent(),

            DealDeleteStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("deal_not_found", $"No offer with id '{id}'."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "deal_published",
                "This offer is published, so it cannot be deleted. Unpublish it instead."))),
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
            $"'{field}' is not a value this offers list understands.",
            details)));
    }
}