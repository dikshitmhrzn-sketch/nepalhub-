using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Ownership;

/// <summary>
/// The listings one person manages, the editor for them, and the two ways a listing of
/// their own reaches the directory: registering it, and asking staff to check it.
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
    BusinessVerificationService verificationService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The account page's ownership screen: every listing this person manages, drafts
    /// included, plus every claim they have filed and every verification they have asked
    /// for, with where each one stands.
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

    /// <summary>
    /// Registers a listing of the caller's own. The row is born owned, unpublished and
    /// unverified — the only thing this grants is the authorship of a draft, and everything
    /// else is what the verification queue exists to decide. Bounded per IP like filing a
    /// claim, because registration opens a row in that same staff queue.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Business)]
    [ProducesResponseType<ApiResponse<OwnerBusinessResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<OwnerBusinessResponse>>> Register(
        RegisterBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await businessService.RegisterAsync(request, user.Id, cancellationToken);

        return outcome.Status switch
        {
            RegisterStatus.Created => Created(
                $"/api/account/businesses/{outcome.Business!.Slug}",
                ApiResponse<OwnerBusinessResponse>.Ok(outcome.Business)),

            RegisterStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another listing."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The listing could not be registered.", outcome.FieldErrors))),
        };
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
    /// The owner's edit: contact details, description and opening hours, and — while the
    /// listing is still a draft — the slug, name, category and district they registered it
    /// with, so a typo made during registration can be corrected without waiting for staff.
    /// The publish flag and the verify badge are not part of this body at all; those remain
    /// staff decisions an owner may never express.
    /// </summary>
    [HttpPut("{slug}")]
    [ProducesResponseType<ApiResponse<OwnerBusinessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
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

        if (outcome.Status == OwnerEditStatus.SlugTaken)
        {
            return Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another listing.")));
        }

        return Ok(ApiResponse<OwnerBusinessResponse>.Ok(outcome.Business!));
    }

    /// <summary>
    /// Asks staff to check this listing: the draft enters the verification queue, and an
    /// approval is what gives it the badge and the public address. A listing that is
    /// already live answers 409 — there is nothing left to approve — and a second request
    /// while one is still waiting answers 409 as well, so the queue holds one question per
    /// listing at a time.
    /// </summary>
    [HttpPost("{slug}/verification")]
    [EnableRateLimiting(RateLimitPolicies.Business)]
    [ProducesResponseType<ApiResponse<VerificationResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> SubmitVerification(
        string slug,
        SubmitVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await verificationService.SubmitAsync(user.Id, slug, request.Note, cancellationToken);

        return outcome.Status switch
        {
            VerificationSubmitStatus.Created => Created(
                $"/api/account/businesses/{slug}",
                ApiResponse<VerificationResponse>.Ok(outcome.Verification!)),

            VerificationSubmitStatus.AlreadyPending => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "verification_pending",
                    "A request for this listing is already waiting for staff. There is nothing to send again."))),

            VerificationSubmitStatus.AlreadyPublished => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "already_published",
                    "This listing is already live, so staff have nothing left to approve."))),

            _ => NotFound(ApiResponse<object>.Fail(
                new ApiError("business_not_found", "That listing is not available."))),
        };
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));

    private NotFoundObjectResult NotFoundOwned() =>
        NotFound(ApiResponse<object>.Fail(
            new ApiError("business_not_found", "That listing is not available.")));
}
