using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Businesses;

/// <summary>
/// The staff queue for owner-registered listings.
///
/// Staff rather than admin: deciding whether a listing is fit to show is moderation work,
/// the same line that lets moderators verify a staff-written listing and settle a claim.
/// The role an approval hands out is the business role — content-level, and nothing like
/// the role administration screen, which is admin-only because it can hand out the
/// moderator and administrator roles themselves.
///
/// <para>
/// Approving writes the audit entry through the shared <see cref="Features.Admin.IAuditLog"/>,
/// which is what makes the decision visible on the audit screen afterwards.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/business-verifications")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class BusinessVerificationsAdminController(
    BusinessVerificationService verificationService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The queue. Without a status filter it reads as work to do: pending requests first,
    /// newest first within each band. The status argument parses as a string so a typo
    /// produces a field error the screen can explain instead of silently returning everything.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<AdminVerificationSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminVerificationSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!VerificationFilters.TryParseStatus(status, out var selectedStatus, out var field))
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

        var result = await verificationService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            selectedStatus,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AdminVerificationSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// Verifies and publishes the listing when it satisfies the publish rules, grants the
    /// owner the business role and writes the audit entry. A listing that has gained a
    /// publish blocker since it was submitted answers 409 with those blockers rather than
    /// going out half-checked.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ApiResponse<AdminVerificationSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminVerificationSummaryResponse>>> Approve(
        Guid id,
        CloseVerificationRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: true, request.ReviewNote, cancellationToken);

    /// <summary>Turns the request down; the listing stays a draft with no badge.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ApiResponse<AdminVerificationSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminVerificationSummaryResponse>>> Reject(
        Guid id,
        CloseVerificationRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: false, request.ReviewNote, cancellationToken);

    private async Task<ActionResult<ApiResponse<AdminVerificationSummaryResponse>>> ReviewAsync(
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
            ? await verificationService.ApproveAsync(
                id, reviewNote, staff.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken)
            : await verificationService.RejectAsync(
                id, reviewNote, staff.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return outcome.Status switch
        {
            VerificationReviewStatus.Approved or VerificationReviewStatus.Rejected => Ok(
                ApiResponse<AdminVerificationSummaryResponse>.Ok(outcome.Verification!)),

            VerificationReviewStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("verification_not_found", $"No verification request with id '{id}'."))),

            VerificationReviewStatus.AlreadyReviewed => Conflict(ApiResponse<object>.Fail(
                new ApiError("verification_closed", "That request has already been decided."))),

            VerificationReviewStatus.NotPublishable => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "not_publishable",
                    "This listing does not satisfy the publish rules yet, so it cannot be approved as it stands.",
                    outcome.Errors!))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "role_grant_failed",
                "The request could not be approved because the business role could not be granted. Nothing was changed; try again, or grant the 'business' role to the account on the accounts screen first."))),
        };
    }
}
