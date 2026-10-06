using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Bookings;

/// <summary>
/// Taking and releasing a place at an event.
///
/// <para>
/// Its own controller rather than actions on <c>EventsController</c>, because that one is
/// class-level <c>[AllowAnonymous]</c> — a personal write there would run without a session by
/// attribute, exactly as the claim controller's comment says. The fallback policy supplies the
/// signed-in member this needs without naming a role: anyone with an account can book.
/// </para>
///
/// <para>
/// The route sits on the event's own address rather than under <c>/account</c>: booking is an
/// act on the event, and the page that offers the button is the event's. What the attendee
/// later reviews still lives at <c>/api/account/bookings</c>, where it can be listed across
/// events.
/// </para>
/// </summary>
[ApiController]
[Route("api/events")]
[Produces("application/json")]
public sealed class EventBookingsController(
    BookingService bookingService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Books places. Full, already-booked and already-started all answer 409 with a code the
    /// page can turn into a sentence, because each is a normal thing for a reader to attempt
    /// rather than a broken request. Success reports the booking as it now stands, so the
    /// page re-renders from the one shape it already knows.
    /// </summary>
    [HttpPost("{slug}/booking")]
    [EnableRateLimiting(RateLimitPolicies.Booking)]
    [ProducesResponseType<ApiResponse<BookingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Book(
        string slug,
        BookEventRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await bookingService.BookAsync(user.Id, slug, request.Quantity, cancellationToken);

        return outcome.Status switch
        {
            BookStatus.Booked => Ok(ApiResponse<BookingResponse>.Ok(outcome.Booking!)),

            BookStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError(
                    "event_not_found",
                    "That event is not available. It may have been retracted, or the link may be out of date."))),

            BookStatus.EventStarted => Conflict(ApiResponse<object>.Fail(
                new ApiError("event_started", "This event has already started, so it is closed for bookings."))),

            BookStatus.AlreadyBooked => Conflict(ApiResponse<object>.Fail(
                new ApiError("already_booked", "You already hold a place at this event."))),

            BookStatus.PaymentRequired => Conflict(ApiResponse<object>.Fail(new ApiError(
                "payment_required",
                "This event charges for a place, so a place is bought rather than booked."))),

            _ => Conflict(ApiResponse<object>.Fail(
                new ApiError("event_full", "This event is fully booked."))),
        };
    }

    /// <summary>
    /// Gives the place back. The row stays with its cancellation stamped on it rather than
    /// being deleted, so a rebooking later finds the same row and the history of who had a
    /// place survives a cancellation.
    /// </summary>
    [HttpDelete("{slug}/booking")]
    [EnableRateLimiting(RateLimitPolicies.Booking)]
    [ProducesResponseType<ApiResponse<BookingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Cancel(
        string slug,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return SessionInvalid();
        }

        var outcome = await bookingService.CancelAsync(user.Id, slug, cancellationToken);

        return outcome.Status switch
        {
            CancelBookingStatus.Cancelled => Ok(ApiResponse<BookingResponse>.Ok(outcome.Booking!)),

            CancelBookingStatus.EventStarted => Conflict(ApiResponse<object>.Fail(
                new ApiError(
                    "event_started",
                    "This event has already started, so a place can no longer be released. Contact the organisers."))),

            _ => NotFound(ApiResponse<object>.Fail(
                new ApiError("booking_not_found", "You do not hold a place at this event."))),
        };
    }

    private UnauthorizedObjectResult SessionInvalid() =>
        Unauthorized(ApiResponse<object>.Fail(
            new ApiError("session_invalid", "The session is no longer valid.")));
}
