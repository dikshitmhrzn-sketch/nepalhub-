using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Businesses;

namespace NepalHub.Api.Features.Events;

/// <summary>
/// The public events calendar.
///
/// A class-level <c>[AllowAnonymous]</c> is safe here for the same reason it is on the businesses and
/// jobs controllers: every action on this controller is a public read. Nothing in this phase is
/// per-visitor, because saved events and accounts are later phases and there are no owner users. If a
/// personal action is ever added here it must be a separate controller with the attribute removed, since
/// the attribute is collected across all endpoint metadata.
/// </summary>
[ApiController]
[Route("api/events")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class EventsController(EventService eventService) : ControllerBase
{
    /// <summary>
    /// What is on and what is coming. Finished events are excluded by the service, not by anything the
    /// caller can influence. An event already under way stays on the list until it ends, so this is the
    /// calendar a reader can open during an event and still find it.
    ///
    /// The filter and sort arguments are read as strings and parsed here rather than bound as enums, so
    /// an unrecognised value produces a specific <c>category_invalid</c> the UI can explain instead of a
    /// framework binding message about a parameter. The location filters are reference codes rather than
    /// ids, which is what the reference endpoints publish and means the client never handles a GUID.
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<EventSummaryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<EventSummaryResponse>>>> List(
        int? page,
        int? pageSize,
        string? search,
        string? province,
        string? district,
        string? category,
        string? sort,
        CancellationToken cancellationToken)
    {
        if (!BusinessFilters.TryParseEnum<EventCategory>(category, nameof(category), out var selectedCategory, out var categoryField))
        {
            return BadFilter(categoryField);
        }

        if (!BusinessFilters.TryParseEnum<EventSort>(sort, nameof(sort), out var parsedSort, out var sortField))
        {
            return BadFilter(sortField);
        }

        var location = await eventService.ResolveLocationFilterAsync(province, district, cancellationToken);
        if (location.InvalidField is { } invalidField)
        {
            return BadFilter(invalidField);
        }

        var query = new EventListQuery(
            PageRequest.Normalise(page, pageSize),
            search,
            location.ProvinceId,
            location.DistrictId,
            selectedCategory,
            parsedSort ?? EventSort.Soonest);

        var result = await eventService.ListAsync(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<EventSummaryResponse>>.Ok(result));
    }

    /// <summary>
    /// One event. A draft, a finished event, and a slug that never existed all return the same 404, so
    /// this endpoint cannot be used to discover which slugs are real but not currently on the calendar.
    [HttpGet("{slug}")]
    [ProducesResponseType<ApiResponse<EventDetailResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EventDetailResponse>>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await eventService.FindBySlugAsync(slug, cancellationToken);

        if (calendarEvent is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("event_not_found", "That event is not available. It may have finished, or the link may be out of date.")));
        }

        return Ok(ApiResponse<EventDetailResponse>.Ok(calendarEvent));
    }

    private BadRequestObjectResult BadFilter(string field)
    {
        IReadOnlyList<FieldError> details =
        [
            new FieldError(field, "invalid", "Choose one of the offered values."),
        ];

        return BadRequest(ApiResponse<object>.Fail(new ApiError(
            $"{field}_invalid",
            $"'{field}' is not a value this calendar understands.",
            details)));
    }
}