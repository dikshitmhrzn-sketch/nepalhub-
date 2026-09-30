using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Admin;

[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class AdminController : ControllerBase
{
    [HttpGet("ping")]
    [ProducesResponseType<ApiResponse<AdminPingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<ApiResponse<AdminPingResponse>> Ping() =>
        Ok(ApiResponse<AdminPingResponse>.Ok(
            new AdminPingResponse("pong", User.Identity?.Name ?? string.Empty, DateTimeOffset.UtcNow)));
}

public sealed record AdminPingResponse(string Status, string SignedInAs, DateTimeOffset Timestamp);
