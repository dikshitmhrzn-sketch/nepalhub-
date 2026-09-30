using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Account;

[ApiController]
[Route("api/account")]
[Produces("application/json")]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
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
}
