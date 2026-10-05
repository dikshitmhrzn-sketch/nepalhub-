using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Features.Auth;

namespace NepalHub.Api.Features.Bookings;

/// <summary>
/// The attendee list behind one event.
///
/// Staff only — the same line the rest of the event board draws. It lives apart from
/// <c>EventsAdminController</c> so the two route prefixes can share an address without their
/// actions competing for it: attendance is a different question from editing, and it is the
/// question that only exists once people have booked.
/// </summary>
[ApiController]
[Route("api/admin/events")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class EventBookingsAdminController(BookingService bookingService) : ControllerBase
{
    /// <summary>
    /// Who holds a place at one event, with the live counter above the rows. An unknown id
    /// answers 404, and a draft with no bookings answers 200 with an empty list — a draft is
    /// a real event staff already know about, unlike anything public-facing.
    /// </summary>
    [HttpGet("{id:guid}/bookings")]
    [ProducesResponseType<ApiResponse<EventAttendanceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EventAttendanceResponse>>> List(
        Guid id,
        CancellationToken cancellationToken)
    {
        var attendance = await bookingService.ListAttendanceAsync(id, cancellationToken);

        return attendance is null
            ? NotFound(ApiResponse<object>.Fail(
                new ApiError("event_not_found", "No event with that id.")))
            : Ok(ApiResponse<EventAttendanceResponse>.Ok(attendance));
    }
}
