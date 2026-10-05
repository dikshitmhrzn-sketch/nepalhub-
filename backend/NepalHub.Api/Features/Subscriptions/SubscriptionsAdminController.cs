using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Subscriptions;

/// <summary>
/// Direct grants, extensions and cancellations: the admin side of the plans.
///
/// Admin only rather than staff. A grant is the closest thing this phase has to taking
/// money — it hands out a paid placement — and that is the same line the accounts screen
/// draws around its own decisions.
/// </summary>
[ApiController]
[Route("api/admin/subscriptions")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class SubscriptionsAdminController(
    SubscriptionService subscriptionService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Grants or extends a plan for one listing, by the same extension rule every other path
    /// uses: a live window rolls forward from where it already ends, an expired one from
    /// today. Extending a listing that is already on the plan also moves it to the newly
    /// granted tier, so granting featured over a free window converts it rather than stacking
    /// a second row beneath the first.
    /// </summary>
    [HttpPost("grant")]
    [ProducesResponseType<ApiResponse<SubscriptionStateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SubscriptionStateResponse>>> Grant(
        GrantSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await userManager.GetUserAsync(User);

        if (admin is null)
        {
            return SessionInvalid();
        }

        var outcome = await subscriptionService.GrantAsync(
            admin.Id,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            SubscribeStatus.NotFound => NotFoundBusiness(),

            SubscribeStatus.PlanUnavailable => Conflict(ApiResponse<object>.Fail(
                new ApiError("plan_unavailable", "That plan cannot be granted right now."))),

            _ => Ok(ApiResponse<SubscriptionStateResponse>.Ok(outcome.State!)),
        };
    }

    /// <summary>Stops a listing's window now; the subscription row is kept as the record.</summary>
    [HttpPost("{businessId:guid}/cancel")]
    [ProducesResponseType<ApiResponse<SubscriptionStateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SubscriptionStateResponse>>> Cancel(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var admin = await userManager.GetUserAsync(User);

        if (admin is null)
        {
            return SessionInvalid();
        }

        var outcome = await subscriptionService.CancelAsync(
            admin.Id,
            businessId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            SubscribeStatus.NotFound => NotFoundBusiness(),

            SubscribeStatus.NotActive => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "subscription_not_active",
                    "This listing has no active subscription to cancel."))),

            _ => Ok(ApiResponse<SubscriptionStateResponse>.Ok(outcome.State!)),
        };
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));

    private NotFoundObjectResult NotFoundBusiness() =>
        NotFound(ApiResponse<object>.Fail(new ApiError("business_not_found", "No listing with that id.")));
}
