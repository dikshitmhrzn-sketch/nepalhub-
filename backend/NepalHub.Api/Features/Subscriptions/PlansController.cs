using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;

namespace NepalHub.Api.Features.Subscriptions;

/// <summary>
/// The public plan catalogue: what a listing can be on and what each plan costs, as a
/// catalogue figure. Anonymous, because a reader deciding whether a business is worth
/// trusting should be able to see what the badges are paid for without an account.
/// </summary>
[ApiController]
[Route("api/plans")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class PlansController(SubscriptionService subscriptionService) : ControllerBase
{
    /// <summary>Every active plan, cheapest first. Inactive ones are hidden rather than labelled.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PlanResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PlanResponse>>>> List(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<PlanResponse>>.Ok(
            await subscriptionService.ListPlansAsync(cancellationToken)));
}
