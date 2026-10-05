using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Moderation;

/// <summary>
/// A member reporting a listing they can see.
///
/// No <c>[AllowAnonymous]</c> appears here: the fallback policy already requires a
/// signed-in reader, and filing a report is exactly the kind of action that should have
/// an account behind it — an anonymous report has nobody to follow up with and nothing
/// to stop one visitor filing hundreds.
/// </summary>
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(
    ReportService reportService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Files one report. A target that is not publicly visible answers 404 rather than
    /// 403, so this endpoint cannot be used to probe for listings that exist but are not
    /// published, and a duplicate answers 409 because the record of the complaint is
    /// already in the queue.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Report)]
    [ProducesResponseType<ApiResponse<ReportResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ReportResponse>>> Create(
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var reporter = await userManager.GetUserAsync(User);

        if (reporter is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var outcome = await reportService.CreateAsync(
            reporter.Id,
            request.TargetType!.Value,
            request.TargetId!.Value,
            request.Reason!.Value,
            request.Note,
            cancellationToken);

        return outcome.Status switch
        {
            ReportCreateStatus.Created => Created(
                $"/api/reports/{outcome.Report!.Id}",
                ApiResponse<ReportResponse>.Ok(ReportViews.Of(outcome.Report))),

            ReportCreateStatus.TargetNotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("target_not_found", "That listing is not available to report."))),

            _ => Conflict(ApiResponse<object>.Fail(
                new ApiError("already_reported", "You have already reported this listing."))),
        };
    }

    /// <summary>
    /// One of the caller's own reports. Somebody else's id and an id that never existed
    /// answer the same way, so this cannot be used to read the queue one row at a time.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<ReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReportResponse>>> GetOwn(
        Guid id,
        CancellationToken cancellationToken)
    {
        var reporter = await userManager.GetUserAsync(User);

        if (reporter is null)
        {
            return Unauthorized();
        }

        var report = await reportService.FindOwnAsync(id, reporter.Id, cancellationToken);

        if (report is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("report_not_found", "That report is not available.")));
        }

        return Ok(ApiResponse<ReportResponse>.Ok(ReportViews.Of(report)));
    }
}
