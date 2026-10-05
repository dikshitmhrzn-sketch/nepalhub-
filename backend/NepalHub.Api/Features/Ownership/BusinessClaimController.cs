using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Ownership;

/// <summary>
/// Filing a claim on a listing.
///
/// <para>
/// Its own controller rather than an action on <c>BusinessesController</c>, because that one
/// is class-level <c>[AllowAnonymous]</c> — a personal action there would run without a
/// session by attribute, and the comment on that controller says the same thing. The claim
/// needs a signed-in member, which the fallback policy supplies without naming a role.
/// </para>
///
/// <para>
/// The endpoint is rate-limited per IP on the same reasoning as filing a report: a claim is
/// a write against a staff queue, and a loop of them is exactly the flood the queue exists
/// to not be handed.
/// </para>
/// </summary>
[ApiController]
[Route("api/businesses")]
[Produces("application/json")]
public sealed class BusinessClaimController(
    OwnershipService ownershipService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Claims one published listing. A draft and an unknown address both answer 404, so this
    /// cannot be used to discover listings staff have not published, and an already-managed
    /// or already-claimed listing answers 409 with a code the screen can explain.
    /// </summary>
    [HttpPost("{slug}/claim")]
    [EnableRateLimiting(RateLimitPolicies.Claim)]
    [ProducesResponseType<ApiResponse<ClaimResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ClaimResponse>>> Claim(
        string slug,
        ClaimBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var outcome = await ownershipService.ClaimAsync(
            user.Id,
            slug,
            request.Evidence,
            cancellationToken);

        return outcome.Status switch
        {
            ClaimSubmitStatus.Created => Created(
                "/api/account/businesses",
                ApiResponse<ClaimResponse>.Ok(outcome.Claim!)),

            ClaimSubmitStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError(
                    "business_not_found",
                    "That listing is not available. It may have been retracted, or the link may be out of date."))),

            ClaimSubmitStatus.AlreadyOwner => Conflict(ApiResponse<object>.Fail(
                new ApiError("already_owner", "You already manage this listing."))),

            ClaimSubmitStatus.AlreadyOwned => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "already_owned",
                    "This listing already has an owner, so it cannot be claimed. If the details are wrong, report the listing instead."))),

            _ => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "already_claimed",
                    "You already have a claim waiting on this listing. Staff review claims before ownership changes."))),
        };
    }
}
