using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Ownership;

/// <summary>
/// The staff queue for ownership claims.
///
/// Staff rather than admin: deciding who a listing belongs to is moderation work, the same
/// line that lets moderators verify a listing. The role an approval hands out is the business
/// role — content-level, and nothing like the role administration screen, which is admin-only
/// because it can hand out the moderator and administrator roles themselves.
///
/// <para>
/// Approving writes the audit entry through the shared <see cref="Features.Admin.IAuditLog"/>,
/// which is what makes the decision visible on the audit screen afterwards.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/business-claims")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class BusinessClaimsAdminController(
    OwnershipService ownershipService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The queue. Without a status filter it reads as work to do: pending claims first,
    /// newest first within each band. The status argument parses as a string so a typo
    /// produces a field error the screen can explain instead of silently returning everything.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<AdminClaimSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminClaimSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!ClaimFilters.TryParseStatus(status, out var selectedStatus, out var field))
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

        var result = await ownershipService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            selectedStatus,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AdminClaimSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// Grants the listing to the claimant, settles the rival claims, adds the business role
    /// and writes the audit entry. A listing that gained a different owner in the meantime
    /// answers 409 rather than quietly moving ownership again.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ApiResponse<AdminClaimSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminClaimSummaryResponse>>> Approve(
        Guid id,
        CloseClaimRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: true, request.ReviewNote, cancellationToken);

    /// <summary>Turns the claim down; the listing stays unowned.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ApiResponse<AdminClaimSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminClaimSummaryResponse>>> Reject(
        Guid id,
        CloseClaimRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: false, request.ReviewNote, cancellationToken);

    private async Task<ActionResult<ApiResponse<AdminClaimSummaryResponse>>> ReviewAsync(
        Guid id,
        bool approve,
        string? reviewNote,
        CancellationToken cancellationToken)
    {
        var staff = await userManager.GetUserAsync(User);

        if (staff is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var outcome = approve
            ? await ownershipService.ApproveAsync(
                id, reviewNote, staff.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken)
            : await ownershipService.RejectAsync(
                id, reviewNote, staff.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return outcome.Status switch
        {
            ClaimReviewStatus.Approved or ClaimReviewStatus.Rejected => Ok(
                ApiResponse<AdminClaimSummaryResponse>.Ok(outcome.Claim!)),

            ClaimReviewStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("claim_not_found", $"No claim with id '{id}'."))),

            ClaimReviewStatus.AlreadyReviewed => Conflict(ApiResponse<object>.Fail(
                new ApiError("claim_closed", "That claim has already been decided."))),

            ClaimReviewStatus.BusinessAlreadyOwned => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "business_already_owned",
                    "This listing gained a different owner while the claim was waiting, so it can no longer be granted."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "role_grant_failed",
                "The claim could not be approved because the business role could not be granted. Nothing was changed; try again, or grant the 'business' role to the account on the accounts screen first."))),
        };
    }
}
