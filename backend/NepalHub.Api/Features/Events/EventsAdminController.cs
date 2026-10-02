using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Events;

/// <summary>
/// Staff management of the events calendar. It sits in this module's folder beside the public controller
/// so the slice stays in one place, exactly as the quiz, resource, job and business admin controllers do.
///
/// Every action here is behind the staff policy, and the middleware chain already requires a CSRF token
/// for the unsafe ones, so neither concern is handled a second time in this file.
/// </summary>
[ApiController]
[Route("api/admin/events")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.StaffOnly)]
public sealed class EventsAdminController(
    EventService eventService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// The staff calendar: drafts included, plus a status filter a public reader has no equivalent of.
    /// Finished events stay in the list with a flag rather than disappearing, because the question a
    /// staff member brings to this screen is usually why something is not showing.
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<EventAdminSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<EventAdminSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? category,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!BusinessFilters.TryParseEnum<EventCategory>(category, nameof(category), out var selectedCategory, out var categoryField))
        {
            return BadFilter(categoryField);
        }

        if (!BusinessFilters.TryParseEnum<EventAdminFilter>(status, nameof(status), out var selectedStatus, out var statusField))
        {
            return BadFilter(statusField);
        }

        var location = await eventService.ResolveLocationFilterAsync(province, district, cancellationToken);
        if (location.InvalidField is { } invalidField)
        {
            return BadFilter(invalidField);
        }

        var result = await eventService.ListForAdminAsync(
            PageRequest.Normalise(page, pageSize),
            search,
            location.ProvinceId,
            location.DistrictId,
            selectedCategory,
            selectedStatus ?? EventAdminFilter.All,
            cancellationToken);

        return Ok(ApiResponse<PagedResult<EventAdminSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// The published businesses an event can be attached to, for the editor's picker. Separate from the
    /// events list because it is a different question with a different answer shape, and it must not be
    /// able to return an unpublished listing: there is nothing to attach to.
    [HttpGet("businesses")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<EventBusinessOptionResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EventBusinessOptionResponse>>>> ListBusinesses(
        string? search,
        CancellationToken cancellationToken)
    {
        var options = await eventService.ListBusinessOptionsAsync(search, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<EventBusinessOptionResponse>>.Ok(options));
    }

    /// <summary>
    /// The full record for the editor. It carries the reasons a draft cannot be published yet, so the
    /// form can show them before staff try to save and are refused.
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<EventAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EventAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await eventService.FindForAdminAsync(id, cancellationToken);

        if (calendarEvent is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("event_not_found", $"No event with id '{id}'.")));
        }

        return Ok(ApiResponse<EventAdminResponse>.Ok(calendarEvent));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<EventAdminResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EventAdminResponse>>> Create(
        SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        var author = await userManager.GetUserAsync(User);
        var outcome = await eventService.CreateAsync(request, author?.Id, cancellationToken);

        return outcome.Status switch
        {
            EventSaveStatus.Created => Created(
                $"/api/admin/events/{outcome.Event!.Id}",
                ApiResponse<EventAdminResponse>.Ok(outcome.Event)),

            EventSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another event."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The event could not be saved.", outcome.FieldErrors))),
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<EventAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<EventAdminResponse>>> Update(
        Guid id,
        SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await eventService.UpdateAsync(id, request, cancellationToken);

        return outcome.Status switch
        {
            EventSaveStatus.Updated => Ok(ApiResponse<EventAdminResponse>.Ok(outcome.Event!)),

            EventSaveStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("event_not_found", $"No event with id '{id}'."))),

            // A conflict rather than a validation failure: the request was well formed, and the state of
            // the row simply no longer allows this particular change.
            EventSaveStatus.SlugTaken => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_taken", outcome.Message ?? "That slug is already used by another event."))),

            EventSaveStatus.SlugImmutable => Conflict(ApiResponse<object>.Fail(new ApiError(
                "slug_immutable", outcome.Message ?? "The slug cannot be changed once an event is published."))),

            _ => BadRequest(ApiResponse<object>.Fail(new ApiError(
                "validation_failed", "The event could not be saved.", outcome.FieldErrors))),
        };
    }

    /// <summary>
    /// Removes a draft. A published row is refused: the intended operation is to retract it, so that the
    /// record of what readers were shown is not simply lost.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var status = await eventService.DeleteAsync(id, cancellationToken);

        return status switch
        {
            EventDeleteStatus.Deleted => NoContent(),

            EventDeleteStatus.NotFound => NotFound(ApiResponse<object>.Fail(
                new ApiError("event_not_found", $"No event with id '{id}'."))),

            _ => Conflict(ApiResponse<object>.Fail(new ApiError(
                "event_published",
                "This event is published, so it cannot be deleted. Unpublish it instead."))),
        };
    }

    private BadRequestObjectResult BadFilter(string field)
    {
        IReadOnlyList<FieldError> details =
        [
            new FieldError(field, "invalid", "Choose one of the offered values."),
        ];

        return BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a value this calendar list understands.",
            details)));
    }
}