using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Badges;

/// <summary>
/// The badges on someone's own account.
///
/// Every action here needs a signed-in user, and gets it from the fallback policy in
/// <c>Program</c> without naming an authorization attribute: this is a person's own
/// progress, so "signed in" is the entire test and there is no role that should widen it.
/// The only verb is GET, so no endpoint in this controller is subject to antiforgery; a
/// badge is granted by finishing a quiz, never by a request that asks for one.
/// </summary>
[ApiController]
[Route("api/account/badges")]
[Produces("application/json")]
public sealed class AccountBadgesController(
    BadgeAwardService badgeAwards,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Every badge with this person's progress against it, earned ones first to carry a
    /// timestamp and locked ones carrying the count still owed.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<ApiResponse<AccountBadgesResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AccountBadgesResponse>>> Get(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(ApiResponse<AccountBadgesResponse>.Ok(
            await badgeAwards.GetBadgesAsync(user.Id, cancellationToken)));
    }
}
