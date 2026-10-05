using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Subscriptions;

/// <summary>
/// The queue of owners asking for a plan.
///
/// Admin only, unlike the claim queue: until a payment provider exists, approving a paid
/// request is the moment the placement is granted, which is the same decision
/// <see cref="SubscriptionsAdminController"/> already reserves for administrators. When a
/// provider's webhook arrives it resolves into this same queue, so approval is written once.
/// </summary>
[ApiController]
[Route("api/admin/subscription-requests")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class SubscriptionRequestsAdminController(
    SubscriptionService subscriptionService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The queue. Without a status filter it reads as work to do: pending requests first,
    /// newest first within each band. The status argument parses as a string so a typo
    /// produces a field error the screen can explain instead of silently returning everything.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<AdminSubscriptionRequestResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminSubscriptionRequestResponse>>>> List(
        int? page,
        int? pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!SubscriptionFilters.TryParseStatus(status, out var selectedStatus, out var field))
        {
            IReadOnlyList<FieldError> details =
            [
                new FieldError(field, "invalid", "Choose one of the offered values."),
            ];

            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                $"{field}_invalid",
                $"'{field}' is not a status this queue understands.",
                details)));
        }

        var result = await subscriptionService.ListRequestsForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            selectedStatus,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AdminSubscriptionRequestResponse>>.Ok(result));
    }

    /// <summary>
    /// Approves a request: the window opens or rolls forward and the request settles in one
    /// save, with a single audit entry naming both. A plan deactivated since the request
    /// answers 409 rather than being granted anyway.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ApiResponse<AdminSubscriptionRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminSubscriptionRequestResponse>>> Approve(
        Guid id,
        CloseSubscriptionRequestRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: true, request.ReviewNote, cancellationToken);

    /// <summary>Turns the request down; nothing is granted.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ApiResponse<AdminSubscriptionRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminSubscriptionRequestResponse>>> Reject(
        Guid id,
        CloseSubscriptionRequestRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: false, request.ReviewNote, cancellationToken);

    private async Task<ActionResult<ApiResponse<AdminSubscriptionRequestResponse>>> ReviewAsync(
        Guid id,
        bool approve,
        string? reviewNote,
        CancellationToken cancellationToken)
    {
        var admin = await userManager.GetUserAsync(User);

        if (admin is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var outcome = approve
            ? await subscriptionService.ApproveRequestAsync(
                id, reviewNote, admin.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken)
            : await subscriptionService.RejectRequestAsync(
                id, reviewNote, admin.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return outcome.Status switch
        {
            RequestDecisionStatus.Approved or RequestDecisionStatus.Rejected => Ok(
                ApiResponse<AdminSubscriptionRequestResponse>.Ok(outcome.Request!)),

            RequestDecisionStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("request_not_found", $"No request with id '{id}'."))),

            RequestDecisionStatus.AlreadyReviewed => Conflict(ApiResponse<object>.Fail(
                new ApiError("request_closed", "That request has already been decided."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "plan_unavailable",
                "That plan is no longer active, so this request cannot be approved. Reject it and ask the owner to pick an available plan."))),
        };
    }
}
