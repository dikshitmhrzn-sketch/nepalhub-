using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Moderation;

/// <summary>
/// The staff queue for member reports.
///
/// Staff rather than admin: working a report is a moderator's job, and the policy here is
/// the same one the seven content controllers sit behind. Closing a report writes an audit
/// entry through the shared <see cref="Features.Admin.IAuditLog"/>, which is what makes the
/// action visible on the admin side afterwards.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class ReportsAdminController(
    ReportService reportService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The queue. Without a status filter it reads as work to do: open reports first,
    /// newest first within each band. The status argument parses as a string so a typo
    /// produces a field error the screen can explain instead of silently returning
    /// everything.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<ReportAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReportAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!ReportFilters.TryParseStatus(status, out var selectedStatus, out var field))
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

        var result = await reportService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            selectedStatus,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<ReportAdminSummaryResponse>>.Ok(result));
    }

    /// <summary>Records the report as acted on, with whatever the moderator noted.</summary>
    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType<ApiResponse<ReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<ReportResponse>>> Resolve(
        Guid id,
        CloseReportRequest request,
        CancellationToken cancellationToken) =>
        CloseAsync(id, ReportStatus.Resolved, request.ResolutionNote, cancellationToken);

    /// <summary>The other way out of the queue: the report was looked at and set aside.</summary>
    [HttpPost("{id:guid}/dismiss")]
    [ProducesResponseType<ApiResponse<ReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<ReportResponse>>> Dismiss(
        Guid id,
        CloseReportRequest request,
        CancellationToken cancellationToken) =>
        CloseAsync(id, ReportStatus.Dismissed, request.ResolutionNote, cancellationToken);

    private async Task<ActionResult<ApiResponse<ReportResponse>>> CloseAsync(
        Guid id,
        ReportStatus status,
        string? resolutionNote,
        CancellationToken cancellationToken)
    {
        var staff = await userManager.GetUserAsync(User);

        if (staff is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var outcome = await reportService.CloseAsync(
            id,
            status,
            resolutionNote,
            staff.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            ReportCloseStatus.Closed => Ok(
                ApiResponse<ReportResponse>.Ok(ReportViews.Of(outcome.Report!))),

            ReportCloseStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("report_not_found", $"No report with id '{id}'."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "report_closed", "That report has already been closed."))),
        };
    }
}
