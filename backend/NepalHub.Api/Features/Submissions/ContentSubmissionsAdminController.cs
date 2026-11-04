using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Submissions;

/// <summary>
/// The staff queue for member-written drafts.
///
/// Staff rather than admin: deciding whether a listing is fit to show is moderation work,
/// the same line that lets a moderator verify a staff-written listing and settle a claim.
/// There is no role an approval hands out — content carries no capability — so the audit
/// line written through <see cref="Features.Admin.IAuditLog"/> is the whole trail of the
/// decision.
///
/// <para>
/// Editing is deliberately absent from this controller: a queued draft is an ordinary
/// unpublished row, so the existing <c>/api/admin/jobs|events|deals/{id}</c> editors
/// already change it, and this queue only reads, approves and turns down.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/content-submissions")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class ContentSubmissionsAdminController(
    ContentSubmissionService submissionService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The queue. Without a status filter it reads as work to do: pending requests first,
    /// newest first within each band. Status and kind parse as strings so a typo produces
    /// a field error the screen can explain instead of silently returning everything.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<AdminSubmissionSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminSubmissionSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? status,
        string? kind,
        CancellationToken cancellationToken)
    {
        if (!SubmissionFilters.TryParseStatus(status, out var selectedStatus, out var statusField))
        {
            return StatusProblem(statusField);
        }

        if (!SubmissionFilters.TryParseKind(kind, out var selectedKind, out var kindField))
        {
            return StatusProblem(kindField);
        }

        var result = await submissionService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            selectedStatus,
            selectedKind,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AdminSubmissionSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// Publishes the draft when it satisfies the publish rules and writes the audit entry.
    /// A draft that has gained a publish blocker since it was submitted answers 409 with
    /// those blockers rather than going out half-checked; a draft that already went live
    /// through the editor is settled without a second publish.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ApiResponse<AdminSubmissionSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminSubmissionSummaryResponse>>> Approve(
        Guid id,
        CloseSubmissionRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: true, request.ReviewNote, cancellationToken);

    /// <summary>Turns the request down; the draft stays unpublished so the author can fix and resend it.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ApiResponse<AdminSubmissionSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<AdminSubmissionSummaryResponse>>> Reject(
        Guid id,
        CloseSubmissionRequest request,
        CancellationToken cancellationToken) =>
        ReviewAsync(id, approve: false, request.ReviewNote, cancellationToken);

    private async Task<ActionResult<ApiResponse<AdminSubmissionSummaryResponse>>> ReviewAsync(
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
            ? await submissionService.ApproveAsync(
                id, reviewNote, staff.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken)
            : await submissionService.RejectAsync(
                id, reviewNote, staff.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return outcome.Status switch
        {
            SubmissionReviewStatus.Approved or SubmissionReviewStatus.Rejected => Ok(
                ApiResponse<AdminSubmissionSummaryResponse>.Ok(outcome.Submission!)),

            SubmissionReviewStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("submission_not_found", $"No submission with id '{id}'."))),

            SubmissionReviewStatus.AlreadyReviewed => Conflict(ApiResponse<object>.Fail(
                new ApiError("submission_closed", "That submission has already been decided."))),

            SubmissionReviewStatus.NotPublishable => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "not_publishable",
                    "This draft does not satisfy the publish rules yet, so it cannot be approved as it stands.",
                    outcome.Errors!))),

            _ => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "already_published",
                    "This draft is already live, so it cannot be turned down from the queue. Unpublish it from the editor if it should not be showing."))),
        };
    }

    private BadRequestObjectResult StatusProblem(string field) =>
        BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a value this queue understands.",
            new List<FieldError>
            {
                new FieldError(field, "invalid", "Choose one of the offered values."),
            })));
}
