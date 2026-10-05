using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Businesses;

/// <summary>
/// The public directory of local businesses.
///
/// A class-level <c>[AllowAnonymous]</c> is safe here for the same reason it is on the job
/// controller: every action on this controller is a public read. Claiming a listing is a
/// personal action, so it lives on <c>Ownership.BusinessClaimController</c> instead — the
/// attribute is collected across all endpoint metadata, and one anonymous action would make
/// every action on this controller anonymous too.
/// </summary>
[ApiController]
[Route("api/businesses")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class BusinessesController(
    BusinessService businessService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The directory. Drafts are excluded by the service, not by anything the caller can influence.
    ///
    /// The filter and sort arguments are read as strings and parsed here rather than bound as
    /// enums, so an unrecognised value produces a specific <c>category_invalid</c> the UI can
    /// explain instead of a framework binding message about a parameter. The location filters are
    /// reference codes rather than ids, which is what the reference endpoints publish and means the
    /// client never handles a GUID.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<BusinessSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<BusinessSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? category,
        string? verified,
        string? sort,
        CancellationToken cancellationToken)
    {
        if (!BusinessFilters.TryParseEnum<BusinessCategory>(category, nameof(category), out var selectedCategory, out var categoryField))
        {
            return BadFilter(categoryField);
        }

        if (!BusinessFilters.TryParseEnum<BusinessSort>(sort, nameof(sort), out var parsedSort, out var sortField))
        {
            return BadFilter(sortField);
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

        var query = new BusinessListQuery(
            PageRequest.Normalise(page, pageSize),
            search,
            location.ProvinceId,
            location.DistrictId,
            selectedCategory,
            parsedVerified,
            parsedSort ?? BusinessSort.Name);

        var result = await businessService.ListAsync(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<BusinessSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// One business. A draft and a slug that never existed both return the same 404, so this
    /// endpoint cannot be used to discover which slugs are real but not yet public. The one
    /// per-visitor field is the caller's own ownership flag, which never widens what is shown.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ApiResponse<BusinessDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BusinessDetailResponse>>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var viewer = await userManager.GetUserAsync(User);
        var business = await businessService.FindBySlugAsync(slug, viewer?.Id, cancellationToken);

        if (business is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("business_not_found", "That listing is not available. It may have been retracted, or the link may be out of date.")));
        }

        return Ok(ApiResponse<BusinessDetailResponse>.Ok(business));
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