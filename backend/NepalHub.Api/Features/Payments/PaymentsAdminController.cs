using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Payments;

/// <summary>
/// The money, from the back office.
///
/// <para>
/// There is no refund button here on purpose: eSewa's returns are issued in the provider's
/// own portal, and this screen exists to notice them — a status check against a row that has
/// already been paid is the move that discovers a refund somebody made elsewhere.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/payments")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public sealed class PaymentsAdminController(
    PaymentService paymentService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// All payments, filtered by what they are waiting for. Without a filter this reads as
    /// work to do: rows that are open or owed back first, newest first within each band.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<AdminPaymentResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminPaymentResponse>>>> List(
        int? page,
        int? pageSize,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!PaymentFilters.TryParseStatus(status, out var selectedStatus, out var field))
        {
            IReadOnlyList<FieldError> details =
            [
                new FieldError(field, "invalid", "Choose one of the offered values."),
            ];

            return BadRequest(ApiResponse<object>.Fail(new ApiError(
                $"{field}_invalid",
                $"'{field}' is not a status this page understands.",
                details)));
        }

        var result = await paymentService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            selectedStatus,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<AdminPaymentResponse>>.Ok(result));
    }

    /// <summary>
    /// Asks the provider about one reference by hand. The answer settles the row when it is
    /// an answer to it — the same question the return page asks, and the only way a refund
    /// issued in the provider's portal ever becomes visible here.
    /// </summary>
    [HttpPost("{reference}/check-status")]
    [ProducesResponseType<ApiResponse<PaymentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentResponse>>> CheckStatus(
        string reference,
        CancellationToken cancellationToken)
    {
        var admin = await userManager.GetUserAsync(User);

        if (admin is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("session_invalid", "The session is no longer valid.")));
        }

        var outcome = await paymentService.CheckStatusAsync(
            admin.Id,
            reference,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (outcome.Status == VerifyStatus.NotFound)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("payment_not_found", $"No payment with reference '{reference}'.")));
        }

        return Ok(ApiResponse<PaymentResponse>.Ok(outcome.Payment!));
    }
}
