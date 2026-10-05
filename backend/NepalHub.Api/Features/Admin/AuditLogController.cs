using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Admin;

/// <summary>
/// Reading the audit trail.
///
/// Admin only, not staff: the log records who did what to other accounts, which is the
/// kind of record that should be readable by the people who oversee the moderators rather
/// than by every moderator. It is read-only by construction — rows are appended by the
/// services and never updated, so there is no action for a controller here to expose.
/// </summary>
[ApiController]
[Route("api/admin/audit-log")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class AuditLogController(IAuditLog auditLog) : ControllerBase
{
    /// <summary>
    /// One page of the trail, newest first. The action and subject filters are exact
    /// matches rather than searches: this screen answers "what happened to this report",
    /// not "find me something like this".
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<AuditLogResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogResponse>>>> List(
        int? page,
        int? pageSize,
        string? action,
        string? targetType,
        CancellationToken cancellationToken)
    {
        var result = await auditLog.ListAsync(
            PageRequest.Normalise(page, pageSize),
            TrimOrNull(action),
            TrimOrNull(targetType),
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AuditLogResponse>>.Ok(result));
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
