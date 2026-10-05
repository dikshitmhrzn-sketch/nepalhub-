using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Subscriptions;

/// <summary>
/// The featured strip above the directory.
///
/// Its own controller rather than an action on <c>BusinessesController</c> so that class
/// stays a single-purpose public read, but it shares that route prefix deliberately: the
/// strip belongs to the directory's address space, and a literal <c>featured</c> segment
/// beats the <c>{slug}</c> parameter beside it — which is also why no listing may use that
/// slug, a rule the staff save enforces.
/// </summary>
[ApiController]
[Route("api/businesses")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class FeaturedBusinessesController(BusinessService businessService) : ControllerBase
{
    /// <summary>
    /// Up to six published, verified listings with a live featured window, alphabetical.
    /// Empty is a normal answer — nobody's slot covers today — and the directory below is
    /// untouched either way.
    /// </summary>
    [HttpGet("featured")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<BusinessSummaryResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BusinessSummaryResponse>>>> List(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<BusinessSummaryResponse>>.Ok(
            await businessService.ListFeaturedAsync(cancellationToken)));
}
