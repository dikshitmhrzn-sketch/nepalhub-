using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Ownership;

/// <summary>
/// The listings one person manages, and the editor for them.
///
/// Signed in only — no role — because this is a person's own screen: the fallback policy in
/// <c>Program</c> is the entire test, and every read and write here is scoped to the caller's
/// own ownership rather than to any permission that could widen it. Somebody else's listing
/// answers the same 404 as one that never existed, so this controller cannot be used to
/// discover which addresses are real.
/// </summary>
[ApiController]
[Route("api/account/businesses")]
[Produces("application/json")]
public sealed class AccountBusinessesController(
    OwnershipService ownershipService,
    BusinessService businessService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The account page's ownership screen: every listing this person manages, drafts
    /// included, plus every claim they have filed and where each one stands.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<MyBusinessesResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<MyBusinessesResponse>>> List(
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var result = await ownershipService.ListMineAsync(user.Id, cancellationToken);

        return Ok(ApiResponse<MyBusinessesResponse>.Ok(result));
    }

    /// <summary>One owned listing in full, for the owner's editor.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ApiResponse<OwnerBusinessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OwnerBusinessResponse>>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var business = await businessService.FindOwnedAsync(slug, user.Id, cancellationToken);

        if (business is null)
        {
            return NotFoundOwned();
        }

        return Ok(ApiResponse<OwnerBusinessResponse>.Ok(business));
    }

    /// <summary>
    /// The owner's edit: contact details, description and opening hours, and nothing else.
    /// Name, slug, category, district and the publish and verify flags are staff's — an owner
    /// corrects what a listing says, not what it is or whether staff stand behind it.
    /// </summary>
    [HttpPut("{slug}")]
    [ProducesResponseType<ApiResponse<OwnerBusinessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OwnerBusinessResponse>>> Update(
        string slug,
        SaveOwnerBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await businessService.UpdateOwnedAsync(slug, user.Id, request, cancellationToken);

        if (outcome.Status == OwnerEditStatus.NotFound)
        {
            return NotFoundOwned();
        }

        if (outcome.Status == OwnerEditStatus.Invalid)
        {
            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed",
                "The request could not be saved.",
                outcome.FieldErrors!)));
        }

        return Ok(ApiResponse<OwnerBusinessResponse>.Ok(outcome.Business!));
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));

    private NotFoundObjectResult NotFoundOwned() =>
        NotFound(ApiResponse<object>.Fail(
            new ApiError("business_not_found", "That listing is not available.")));
}
