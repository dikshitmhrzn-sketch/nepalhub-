using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Subscriptions;

/// <summary>
/// A person's own listing and its standing with the plans.
///
/// Signed in only, with every path scoped to listings the caller owns — the fallback policy
/// is the entire test, and somebody else's listing answers the same 404 as one that never
/// existed, so this cannot be used to read other people's entitlement.
/// </summary>
[ApiController]
[Route("api/account/businesses")]
[Produces("application/json")]
public sealed class AccountSubscriptionController(
    SubscriptionService subscriptionService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Which plan this listing is on, whether the window covers today, and any request still
    /// waiting — all derived on read, so an expired slot reports itself without a job having run.
    /// </summary>
    [HttpGet("{slug}/subscription")]
    [ProducesResponseType<ApiResponse<SubscriptionStateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SubscriptionStateResponse>>> GetState(
        string slug,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var state = await subscriptionService.FindStateAsync(slug, user.Id, cancellationToken);

        return state is null ? NotFoundOwned() : Ok(ApiResponse<SubscriptionStateResponse>.Ok(state));
    }

    /// <summary>
    /// Asks for a plan. The free plan is granted on the spot, because there is nothing to
    /// decide; featured lands in the admin queue and comes back as a pending request on the
    /// same state the screen already renders. Either way the response is the listing's
    /// standing, so the page re-renders from one shape.
    /// </summary>
    [HttpPost("{slug}/subscription-requests")]
    [ProducesResponseType<ApiResponse<SubscriptionStateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SubscriptionStateResponse>>> Subscribe(
        string slug,
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await subscriptionService.SubscribeAsync(
            user.Id,
            slug,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            SubscribeStatus.NotFound => NotFoundOwned(),

            SubscribeStatus.AlreadyPending => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "request_pending",
                    "A request for this listing is already waiting for a decision."))),

            SubscribeStatus.PlanUnavailable => Conflict(ApiResponse<object>.Fail(
                new ApiError("plan_unavailable", "That plan cannot be granted right now."))),

            _ => Ok(ApiResponse<SubscriptionStateResponse>.Ok(outcome.State!)),
        };
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));

    private NotFoundObjectResult NotFoundOwned() =>
        NotFound(ApiResponse<object>.Fail(
            new ApiError("business_not_found", "That listing is not available.")));
}
