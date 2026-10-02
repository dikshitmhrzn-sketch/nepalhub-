using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Deals;

/// <summary>
/// The public offers page.
///
/// A class-level <c>[AllowAnonymous]</c> is safe here for the same reason it is on the businesses and
/// jobs controllers: every action on this controller is a public read. Nothing in this phase is
/// per-visitor, because saved offers and accounts are later phases and there are no owner users. If a
/// personal action is ever added here it must be a separate controller with the attribute removed,
/// since the attribute is collected across all endpoint metadata.
/// </summary>
[ApiController]
[Route("api/deals")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class DealsController(DealService dealService) : ControllerBase
{
    /// <summary>
    /// The active offers. Expired offers are excluded by the service, not by anything the caller can
    /// influence, so there is no way to ask this endpoint for a discount that has finished.
    ///
    /// The filter and sort arguments are read as strings and parsed here rather than bound as enums, so
    /// an unrecognised value produces a specific <c>sort_invalid</c> the UI can explain instead of a
    /// framework binding message about a parameter. The location filters are reference codes rather
    /// than ids, which is what the reference endpoints publish and means the client never handles a
    /// GUID.
    ///
    /// Note there is no category filter here. The offer page filters by place and by date, because that
    /// is what a reader asks about a discount, and adding a category list would mean inventing a
    /// taxonomy for businesses that may not be listed at all.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<DealSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<DealSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? sort,
        CancellationToken cancellationToken)
    {
        if (!BusinessFilters.TryParseEnum<DealSort>(sort, nameof(sort), out var parsedSort, out var sortField))
        {
            return BadFilter(sortField);
        }

        var location = await dealService.ResolveLocationFilterAsync(province, district, cancellationToken);
        if (location.InvalidField is { } invalidField)
        {
            return BadFilter(invalidField);
        }

        var query = new DealListQuery(
            PageRequest.Normalise(page, pageSize),
            search,
            location.ProvinceId,
            location.DistrictId,
            parsedSort ?? DealSort.EndingSoon);

        var result = await dealService.ListAsync(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<DealSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// One offer. A draft, an offer that has ended, and a slug that never existed all return the same
    /// 404, so this endpoint cannot be used to discover which slugs are real but not currently
    /// available.
    [HttpGet("{slug}")]
    [ProducesResponseType<ApiResponse<DealDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DealDetailResponse>>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var deal = await dealService.FindBySlugAsync(slug, cancellationToken);

        if (deal is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("deal_not_found", "That offer is not available. It may have finished, or the link may be out of date.")));
        }

        return Ok(ApiResponse<DealDetailResponse>.Ok(deal));
    }

    private BadRequestObjectResult BadFilter(string field)
    {
        IReadOnlyList<FieldError> details =
        [
            new FieldError(field, "invalid", "Choose one of the offered values."),
        ];

        return BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a value this offers page understands.",
            details)));
    }
}