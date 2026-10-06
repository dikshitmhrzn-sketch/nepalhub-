using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Payments;

/// <summary>
/// Leaving the site to pay, and coming back from it.
///
/// <para>
/// Every action here is a signed-in member: the buyer's own reference is what each one is
/// looked up by, so no endpoint takes a payment id at face value. The one anonymous action
/// is the configuration read, because the listing and event pages have to know whether to
/// offer a checkout button before anybody has signed in to ask for one.
/// </para>
/// </summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController(
    PaymentService paymentService,
    PaymentSettings settings,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Whether checkout exists. Says only that a provider is configured and which one —
    /// never its merchant code, urls, or anything else a stranger could use to imitate it.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<PaymentConfigResponse>>(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<PaymentConfigResponse>> Config() =>
        Ok(ApiResponse<PaymentConfigResponse>.Ok(
            new PaymentConfigResponse(settings.IsConfigured, settings.Provider)));

    /// <summary>
    /// Starts one checkout: takes the seat or reuses the question already being asked,
    /// writes the payment row, and answers with the signed form to post to the provider.
    /// Nothing is charged here — the charge happens on the provider's own pages.
    /// </summary>
    [HttpPost("checkout")]
    [EnableRateLimiting(RateLimitPolicies.Payment)]
    [ProducesResponseType<ApiResponse<CheckoutResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<CheckoutResponse>>> Checkout(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await paymentService.BeginCheckoutAsync(
            user.Id,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            CheckoutStatus.Ready or CheckoutStatus.AlreadySettled => Ok(
                ApiResponse<CheckoutResponse>.Ok(
                    outcome.Checkout ?? new CheckoutResponse(
                        outcome.Payment!.Reference,
                        CheckoutUrl: null,
                        Fields: null,
                        outcome.Payment.Status))),

            CheckoutStatus.NotConfigured => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(new ApiError(
                    "payments_not_configured",
                    "Online payment is not switched on on this site yet, so nothing can be bought here."))),

            CheckoutStatus.BusinessNotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("business_not_found", "That listing is not available for you to buy a plan for."))),

            CheckoutStatus.EventNotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("event_not_found", "That event is not available."))),

            CheckoutStatus.Free => Conflict(ApiResponse<object>.Fail(new ApiError(
                "payment_not_required",
                "There is no price on that, so there is nothing to pay for."))),

            CheckoutStatus.PlanUnavailable => Conflict(ApiResponse<object>.Fail(
                new ApiError("plan_unavailable", "That plan is not available right now."))),

            CheckoutStatus.EventStarted => Conflict(ApiResponse<object>.Fail(
                new ApiError("event_started", "This event has already started, so tickets are closed."))),

            CheckoutStatus.AlreadyBooked => Conflict(ApiResponse<object>.Fail(
                new ApiError("already_booked", "You already hold a place at this event."))),

            _ => Conflict(ApiResponse<object>.Fail(
                new ApiError("event_full", "This event is fully booked."))),
        };
    }

    /// <summary>
    /// The buyer's return. The signed payload, when one came back with them, is checked
    /// and still not believed: the settlement underneath asks the provider directly, so a
    /// buyer who edits the return address or the query string settles nothing.
    /// </summary>
    [HttpPost("verify")]
    [EnableRateLimiting(RateLimitPolicies.Payment)]
    [ProducesResponseType<ApiResponse<PaymentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<PaymentResponse>>> Verify(
        VerifyPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await paymentService.VerifyAsync(
            user.Id,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return outcome.Status switch
        {
            VerifyStatus.Settled or VerifyStatus.Pending => Ok(
                ApiResponse<PaymentResponse>.Ok(outcome.Payment!)),

            VerifyStatus.InvalidSignature => BadRequest(ApiResponse<object>.Fail(
                new ApiError("payment_signature_invalid", "The reply from the payment provider could not be confirmed."))),

            _ => NotFound(ApiResponse<object>.Fail(
                new ApiError("payment_not_found", "No payment with that reference belongs to you."))),
        };
    }

    /// <summary>
    /// One payment of the buyer's own, read back after a refresh or a closed tab. Quiet
    /// ones are reconciled first, so the answer is the provider's and not a stale row.
    /// </summary>
    [HttpGet("{reference}")]
    [ProducesResponseType<ApiResponse<PaymentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentResponse>>> Get(
        string reference,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await paymentService.GetStateAsync(user.Id, reference, cancellationToken);

        if (outcome is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("payment_not_found", "No payment with that reference belongs to you.")));
        }

        return Ok(ApiResponse<PaymentResponse>.Ok(outcome.Payment!));
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));
}
