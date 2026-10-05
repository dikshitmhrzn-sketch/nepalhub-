using System.Net;
using System.Text.Json;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Bookings;
using NepalHub.Api.Features.Events;

namespace NepalHub.Tests;

/// <summary>
/// Shared moves for the booking tests: a fixture event with a known number of places, the
/// take-and-release calls themselves, and the reader's view of how many places are left.
/// </summary>
internal static class BookingApi
{
    /// <summary>
    /// An event staff have just published, with a seat count and a start the test names.
    /// The default starts tomorrow, comfortably on the bookable side of the clock.
    /// </summary>
    public static async Task<EventAdminResponse> CreateEventAsync(
        NepalHubApiFactory factory,
        int? capacity = null,
        int startsInHours = 24,
        int durationHours = 2,
        bool published = true)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var slug = EventApi.NewSlug();

        var created = await new AuthApiClient(staff).PostForSnapshotAsync(
            "/api/admin/events",
            EventApi.Body(
                slug,
                $"Booking Event {slug}",
                published,
                capacity: capacity,
                startsInHours: startsInHours,
                durationHours: durationHours));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return EventApi.ReadCreated(created);
    }

    /// <summary>Takes places and asserts the take was accepted, returning the booking as it stands.</summary>
    public static async Task<BookingResponse> BookAsync(
        HttpClient attendee,
        string slug,
        int quantity = 1)
    {
        var snapshot = await new AuthApiClient(attendee).PostForSnapshotAsync(
            $"/api/events/{slug}/booking",
            new { quantity });

        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        return Read<BookingResponse>(snapshot);
    }

    /// <summary>Releases the caller's place, returning the row as the release left it.</summary>
    public static async Task<BookingResponse> CancelAsync(HttpClient attendee, string slug)
    {
        var snapshot = await new AuthApiClient(attendee).DeleteForSnapshotAsync(
            $"/api/events/{slug}/booking");

        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        return Read<BookingResponse>(snapshot);
    }

    public static async Task<EventDetailResponse> ReadDetailAsync(HttpClient client, string slug) =>
        await new AuthApiClient(client).GetAsync<EventDetailResponse>($"/api/events/{slug}");

    private static T Read<T>(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<T>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }
}

/// <summary>
/// Taking a place: the seat counter is the truth, so every assertion goes through the reader's
/// view of it — how many places the event says are held and how many are left.
/// </summary>
public sealed class BookingFlowTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task Booking_requires_an_account()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(factory, capacity: 10);

        using var client = factory.CreateClient();
        using var response = await new AuthApiClient(client).PostAsync(
            $"/api/events/{calendarEvent.Slug}/booking",
            new { quantity = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Places_are_taken_until_the_event_is_full_and_given_back_on_cancellation()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(factory, capacity: 3);
        var slug = calendarEvent.Slug;

        var firstClient = factory.CreateSignedInClientAsync(TestData.UniqueEmail("seat-a"));
        var secondClient = factory.CreateSignedInClientAsync(TestData.UniqueEmail("seat-b"));
        var thirdClient = factory.CreateSignedInClientAsync(TestData.UniqueEmail("seat-c"));

        var first = await BookingApi.BookAsync(firstClient, slug, 2);
        Assert.Equal(EventBookingStatus.Confirmed, first.Status);

        var detail = await BookingApi.ReadDetailAsync(firstClient, slug);
        Assert.Equal(3, detail.Capacity);
        Assert.Equal(2, detail.RegistrationCount);
        Assert.Equal(1, detail.RemainingSpots);

        var again = await new AuthApiClient(firstClient).PostForSnapshotAsync(
            $"/api/events/{slug}/booking",
            new { quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_booked", again.Error!.Code);

        var tooMany = await new AuthApiClient(secondClient).PostForSnapshotAsync(
            $"/api/events/{slug}/booking",
            new { quantity = 2 });
        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
        Assert.Equal("event_full", tooMany.Error!.Code);

        var last = await BookingApi.BookAsync(secondClient, slug, 1);
        Assert.Equal(1, last.Quantity);

        detail = await BookingApi.ReadDetailAsync(firstClient, slug);
        Assert.Equal(3, detail.RegistrationCount);
        Assert.Equal(0, detail.RemainingSpots);

        var refused = await new AuthApiClient(thirdClient).PostForSnapshotAsync(
            $"/api/events/{slug}/booking",
            new { quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("event_full", refused.Error!.Code);

        detail = await BookingApi.ReadDetailAsync(firstClient, slug);
        Assert.Equal(3, detail.RegistrationCount);

        var released = await BookingApi.CancelAsync(firstClient, slug);
        Assert.Equal(EventBookingStatus.Cancelled, released.Status);

        detail = await BookingApi.ReadDetailAsync(firstClient, slug);
        Assert.Equal(1, detail.RegistrationCount);
        Assert.Equal(2, detail.RemainingSpots);

        var secondRelease = await new AuthApiClient(firstClient).DeleteForSnapshotAsync(
            $"/api/events/{slug}/booking");
        Assert.Equal(HttpStatusCode.NotFound, secondRelease.StatusCode);
        Assert.Equal("booking_not_found", secondRelease.Error!.Code);

        // The cancelled row is the booking again rather than a second row beside it.
        var rebooked = await BookingApi.BookAsync(firstClient, slug, 2);
        Assert.Equal(first.Id, rebooked.Id);
        Assert.Equal(EventBookingStatus.Confirmed, rebooked.Status);

        detail = await BookingApi.ReadDetailAsync(firstClient, slug);
        Assert.Equal(3, detail.RegistrationCount);
        Assert.Equal(0, detail.RemainingSpots);

        foreach (var quantity in new[] { 0, 5 })
        {
            var invalid = await new AuthApiClient(thirdClient).PostForSnapshotAsync(
                $"/api/events/{slug}/booking",
                new { quantity });

            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("validation_failed", invalid.Error!.Code);
            Assert.Contains(
                "quantity",
                invalid.Error.Details!.Value.GetRawText(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task An_event_that_has_already_started_is_closed_for_bookings()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(
            factory,
            capacity: 10,
            startsInHours: -6,
            durationHours: 12);

        using var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("late"));

        var detail = await BookingApi.ReadDetailAsync(client, calendarEvent.Slug);
        Assert.True(detail.IsOngoing);

        var snapshot = await new AuthApiClient(client).PostForSnapshotAsync(
            $"/api/events/{calendarEvent.Slug}/booking",
            new { quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, snapshot.StatusCode);
        Assert.Equal("event_started", snapshot.Error!.Code);
    }

    [Fact]
    public async Task A_draft_and_a_slug_that_never_existed_answer_the_same_404()
    {
        var draft = await BookingApi.CreateEventAsync(factory, capacity: 5, published: false);

        using var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("ghost"));

        foreach (var slug in new[] { draft.Slug, EventApi.NewSlug() })
        {
            var snapshot = await new AuthApiClient(client).PostForSnapshotAsync(
                $"/api/events/{slug}/booking",
                new { quantity = 1 });

            Assert.Equal(HttpStatusCode.NotFound, snapshot.StatusCode);
            Assert.Equal("event_not_found", snapshot.Error!.Code);
        }
    }
}

/// <summary>
/// The staff side of a booking: who is coming. The list is staff-only because attendance is
/// personal data, and it counts holds and cancellations rather than only the live ones.
/// </summary>
public sealed class BookingAttendanceTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task The_attendee_list_is_staff_only_and_carries_the_live_counter()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(factory, capacity: 3);

        var firstEmail = TestData.UniqueEmail("attendee-a");
        var secondEmail = TestData.UniqueEmail("attendee-b");
        var firstClient = factory.CreateSignedInClientAsync(firstEmail);
        var secondClient = factory.CreateSignedInClientAsync(secondEmail);

        await BookingApi.BookAsync(firstClient, calendarEvent.Slug, 2);
        await BookingApi.BookAsync(secondClient, calendarEvent.Slug, 1);

        using var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("nosy"));
        using var refused = await member.GetAsync(
            $"/api/admin/events/{calendarEvent.Id}/bookings");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var attendance = await new AuthApiClient(moderator)
            .GetAsync<EventAttendanceResponse>($"/api/admin/events/{calendarEvent.Id}/bookings");

        Assert.Equal(calendarEvent.Id, attendance.EventId);
        Assert.Equal(3, attendance.Capacity);
        Assert.Equal(3, attendance.RegistrationCount);

        Assert.Equal(2, attendance.Bookings.Count);
        Assert.All(attendance.Bookings, row => Assert.Equal(EventBookingStatus.Confirmed, row.Status));
        Assert.Contains(attendance.Bookings, row => row.AttendeeEmail == firstEmail && row.Quantity == 2);
        Assert.Contains(attendance.Bookings, row => row.AttendeeEmail == secondEmail && row.Quantity == 1);

        using var unknown = await moderator.GetAsync($"/api/admin/events/{Guid.NewGuid()}/bookings");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains("event_not_found", await unknown.Content.ReadAsStringAsync());
    }
}
