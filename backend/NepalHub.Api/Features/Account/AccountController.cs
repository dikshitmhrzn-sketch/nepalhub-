using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Email;

namespace NepalHub.Api.Features.Account;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    EmailDispatchService emailDispatch,
    ILogger<AccountController> logger) : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<ApiResponse<SessionUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SessionUserResponse>>> Me(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();

            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        return Ok(ApiResponse<SessionUserResponse>.Ok(
            await SessionUserMapper.MapAsync(user, userManager, cancellationToken)));
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.ChangePassword)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();

            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            // A wrong current password and a new password the policy refuses come back as
            // the same shape every other password failure in this API gives: a 400 with
            // the field and the code, so the form can say what to fix.
            var errors = IdentityErrors.Translate(result.Errors);
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("validation_failed", "The password could not be changed.", errors)));
        }

        // Identity rotated the security stamp on the way through, which is what actually
        // ends the sessions held elsewhere; the notice is the courtesy that says so, and
        // it fails into a log line like every other message rather than failing the change.
        await emailDispatch.SendPasswordChangedNoticeAsync(user, cancellationToken);
        logger.LogInformation("Password changed for user {UserId}.", user.Id);

        return Ok(ApiResponse<object>.Ok(new { changed = true }));
    }
}
