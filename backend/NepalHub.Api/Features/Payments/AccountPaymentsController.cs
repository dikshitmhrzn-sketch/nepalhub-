using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Payments;

/// <summary>
/// The buyer's own payment history.
///
/// <para>
/// Its own controller rather than actions on <see cref="PaymentsController"/>: that route
/// addresses one payment by reference, this one lists a person's, and the two are different
/// shapes of thing — <c>/api/account</c> is where the rest of the site keeps everything a
/// member reads about themselves.
/// </para>
/// </summary>
[ApiController]
[Route("api/account/payments")]
[Produces("application/json")]
public sealed class AccountPaymentsController(
    PaymentService paymentService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>The signed-in buyer's payments, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<PaymentSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var result = await paymentService.ListForUserAsync(
            user.Id,
            PageRequest.Normalise(page, pageSize),
            cancellationToken);

        return Ok(ApiResponse<PagedResult<PaymentSummaryResponse>>.Ok(result));
    }
}
