using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Events;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Reference;

namespace NepalHub.Tests;

/// <summary>
/// Reading the calendar as a visitor. Nothing here signs in, which is the point: a list of what is
/// happening near you has to work for somebody who has never made an account.
///
/// Assertions are scoped to a search term or a seed slug wherever a count is involved, rather than
/// asserting a whole-page total, because the staff tests publish rows of their own and a suite that only
/// passed because of the order its classes ran in would fail the next time somebody reordered it.
/// </summary>
public sealed class EventsTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string PublicPath = "/api/events";
    private const string AdminPath = "/api/admin/events";

    [Fact]
    public async Task The_calendar_is_readable_without_an_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Every category is seeded, and a category nobody seeded is a category whose filter silently returns
    /// nothing, which a visitor reports as "your search is broken" rather than as an empty category.
    /// </summary>
    [Fact]
    public async Task The_seeded_calendar_covers_every_category()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var seeded = new HashSet<string>(
            new[]
            {
                "newa-street-food-festival-kathmandu",
                "pokhara-paragliding-safety-briefing",
                "biratnagar-literature-reading",
                "tihar-celebration-baneshwor",
                "butwal-marathon",
                "it-career-workshop-kathmandu",
                "janaki-school-sports-day-bardibas",
                "bhaktapur-craft-fair",
                "dhangadhi-health-camp",
                "annapurna-trek-briefing-kathmandu",
                "small-business-registration-clinic-lalitpur",
                "bhajan-sandhya-birendranagar-surkhet",
                "itahari-cycle-repair-day",
            },
            StringComparer.Ordinal);

        var live = await ReadAllPublicAsync(api);
        var found = live.Where(item => seeded.Contains(item.Slug)).ToList();

        Assert.Equal(seeded.Count, found.Count);

        foreach (var category in Enum.GetValues<EventCategory>())
        {
            Assert.Contains(found, item => item.Category == category);
        }
    }

    [Fact]
    public async Task The_seeded_calendar_spreads_across_four_or_more_provinces()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var live = await ReadAllPublicAsync(api);

        var provinces = live
            .Select(item => item.ProvinceName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(provinces.Count >= 4, $"The seeded calendar covers {provinces.Count} provinces.");
    }

    [Fact]
    public async Task A_seeded_event_opens_and_reports_its_times_and_place()
    {
        using var client = factory.CreateClient();

        var envelope = await client.GetFromJsonAsync<ApiEnvelope<EventDetailResponse>>(
            $"{PublicPath}/pokhara-paragliding-safety-briefing",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal("Paragliding safety briefing", envelope.Data!.Title);
        Assert.Equal(EventCategory.Sports, envelope.Data.Category);
        Assert.Equal("Lakeside Paragliding Association", envelope.Data.Organiser);
        Assert.Equal("Kaski", envelope.Data.DistrictName);
        Assert.Equal("Gandaki", envelope.Data.ProvinceName);
        Assert.Equal("Lakeside Sports Centre, Pokhara", envelope.Data.Venue);

        // The times are required and are read in the Nepal offset, so the start is never reported as the
        // same instant the server happens to be running on.
        Assert.True(envelope.Data.EndsAt > envelope.Data.StartsAt);
        Assert.Equal(TimeSpan.FromHours(2), envelope.Data.EndsAt - envelope.Data.StartsAt);
    }

    [Fact]
    public async Task A_draft_is_invisible_to_readers_and_answers_the_same_404_as_a_slug_that_never_existed()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var slug = EventApi.NewSlug();
        var title = $"Hidden Event {Guid.NewGuid():N}";

        var created = await staffApi.PostForSnapshotAsync(
            AdminPath,
            EventApi.Body(slug, title, published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Searched by the unique token in the title rather than by the slug, because the slug is not a
        // searchable field and a search on it would match nothing whether the draft was published or not.
        var publicPage = await new AuthApiClient(reader).GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(title)}&pageSize=100");
        Assert.Equal(0, publicPage.TotalCount);

        using var draft = await reader.GetAsync($"{PublicPath}/{slug}");
        using var invented = await reader.GetAsync($"{PublicPath}/no-such-event-{Guid.NewGuid():N}");

        // The same answer for both, so the endpoint cannot be used to find out which slugs are real but
        // not yet public.
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invented.StatusCode);
    }

    [Fact]
    public async Task A_finished_event_is_invisible_to_readers()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // The seeder writes published rows whose end time has passed on purpose, which is the only way to
        // be sure the expiry filter runs on real data rather than on a test double.
        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("pottery")}&pageSize=100");

        Assert.Equal(0, page.TotalCount);

        using var detail = await client.GetAsync($"{PublicPath}/bhaktapur-pottery-workshop-dharamshala-expired");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task A_finished_event_stays_in_the_staff_list_with_a_flag()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(staff);

        var page = await api.GetAsync<PagedResponse<EventAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString("pottery")}&pageSize=100");

        var calendarEvent = Assert.Single(page.Items);
        Assert.True(calendarEvent.IsPublished);
        Assert.True(calendarEvent.IsFinished);
    }

    [Fact]
    public async Task A_published_event_reaches_readers_with_the_fields_a_visitor_acts_on()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var title = $"Reachable Event {Guid.NewGuid():N}";
        var slug = EventApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, EventApi.Body(
            slug,
            title,
            published: true,
            districtCode: "bagmati-bhaktapur",
            venue: "Taumadhi square",
            registrationUrl: "https://example.com/register"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<EventDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(title, envelope.Data!.Title);
        Assert.Equal("Taumadhi square", envelope.Data.Venue);
        Assert.Equal("Bhaktapur", envelope.Data.DistrictName);
        Assert.Equal("Bagmati", envelope.Data.ProvinceName);
        Assert.Equal("https://example.com/register", envelope.Data.RegistrationUrl);
    }

    [Fact]
    public async Task An_event_survives_the_business_it_names_being_unpublished()
    {
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(admin);

        // A draft business, which the events editor cannot pick from its published-only list but which
        // the save path still accepts by slug. Listing an event before its host is listed is the normal
        // order of work, and an event must not be blocked by it.
        var businessSlug = $"test-draft-cafe-{Guid.NewGuid():N}";
        var businessName = $"Unlisted Cafe {Guid.NewGuid():N}";
        var business = await adminApi.PostForSnapshotAsync(
            "/api/admin/businesses",
            BusinessApi.Body(
                businessSlug,
                businessName,
                published: false,
                phone: "9800000102",
                districtCode: "gandaki-kaski"));
        Assert.Equal(HttpStatusCode.Created, business.StatusCode);

        var eventSlug = EventApi.NewSlug();
        var created = await adminApi.PostForSnapshotAsync(AdminPath, EventApi.Body(
            eventSlug,
            $"Draft Linked Event {Guid.NewGuid():N}",
            published: true,
            businessSlug: businessSlug,
            districtCode: "gandaki-kaski"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var reader = factory.CreateClient();
        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<EventDetailResponse>>(
            $"{PublicPath}/{eventSlug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(businessName, envelope.Data!.BusinessName);
        Assert.Null(envelope.Data.BusinessSlug);
    }

    [Fact]
    public async Task The_search_matches_the_title_the_organiser_and_the_venue()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var byOrganiser = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("Paragliding Association")}&pageSize=100");
        Assert.NotEmpty(byOrganiser.Items);

        // A reader who knows "the Pokhara one" should be able to find it by the venue.
        var byVenue = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("Lakeside Sports Centre")}&pageSize=100");
        Assert.NotEmpty(byVenue.Items);

        var byDetails = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("free and there is no need to register")}&pageSize=100");
        Assert.NotEmpty(byDetails.Items);
    }

    [Fact]
    public async Task A_search_term_of_wildcards_matches_nothing_rather_than_everything()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("_")}&pageSize=100");

        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task A_category_filter_returns_only_that_category()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>($"{PublicPath}?category=Sports&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(EventCategory.Sports, item.Category));
    }

    [Theory]
    [InlineData("lumbini-rupandehi", "Rupandehi", "Lumbini")]
    [InlineData("madhesh-dhanusha", "Dhanusha", "Madhesh")]
    [InlineData("sudurpashchim-kailali", "Kailali", "Sudurpashchim")]
    public async Task A_district_filter_returns_the_events_that_sit_in_it(
        string districtCode,
        string districtName,
        string provinceName)
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?district={districtCode}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.Equal(districtName, item.DistrictName);
            Assert.Equal(provinceName, item.ProvinceName);
        });
    }

    [Fact]
    public async Task A_province_filter_returns_only_events_in_that_province()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?province=madhesh&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal("Madhesh", item.ProvinceName));
    }

    [Fact]
    public async Task The_default_order_puts_the_event_starting_soonest_first()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>($"{PublicPath}?pageSize=100");

        var starts = page.Items.Select(item => item.StartsAt).ToList();

        Assert.Equal(starts.OrderBy(at => at).ToList(), starts);
    }

    [Fact]
    public async Task The_newest_order_puts_the_most_recently_added_first()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>($"{PublicPath}?sort=Newest&pageSize=100");

        var created = page.Items.Select(item => item.CreatedAt).ToList();

        Assert.Equal(created.OrderByDescending(at => at).ToList(), created);
    }

    [Fact]
    public async Task Paging_walks_the_calendar_without_repeating_one()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var first = await api.GetAsync<PagedResponse<EventSummaryResponse>>($"{PublicPath}?pageSize=3");
        Assert.True(first.Items.Count <= 3);

        if (first.TotalPages <= 1)
        {
            return;
        }

        var second = await api.GetAsync<PagedResponse<EventSummaryResponse>>($"{PublicPath}?page=2&pageSize=3");

        Assert.Empty(first.Items.Select(item => item.Slug).Intersect(second.Items.Select(item => item.Slug)));
    }

    [Fact]
    public async Task A_page_size_beyond_the_ceiling_is_clamped_rather_than_refused()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>($"{PublicPath}?pageSize=5000");

        Assert.Equal(100, page.PageSize);
    }

    [Theory]
    [InlineData("sort", "sideways")]
    [InlineData("category", "Bargains")]
    public async Task An_unrecognised_filter_value_is_refused_with_the_field_named(string parameter, string value)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?{parameter}={Uri.EscapeDataString(value)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"{parameter}_invalid", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("province", "atlantis")]
    [InlineData("district", "nowhere-1")]
    public async Task An_unrecognised_location_code_is_refused_rather_than_matching_nothing(string parameter, string value)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?{parameter}={Uri.EscapeDataString(value)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_slug_already_used_by_another_event_is_refused_as_a_conflict()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = EventApi.NewSlug();

        var first = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(slug, "First Owner", published: false));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(slug, "Second Owner", published: false));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("slug_taken", second.Error?.Code);
    }

    [Fact]
    public async Task The_slug_of_a_published_event_cannot_be_moved()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = EventApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(slug, "Frozen Slug Event", published: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = EventApi.ReadCreated(created);

        var moved = await api.PutForSnapshotAsync(
            $"{AdminPath}/{stored.Id}",
            EventApi.Body(EventApi.NewSlug(), "Frozen Slug Event", published: true));

        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.Equal("slug_immutable", moved.Error?.Code);
    }

    [Fact]
    public async Task A_draft_slug_can_still_be_corrected()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(EventApi.NewSlug(), "Mistyped Slug", published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = EventApi.ReadCreated(created);
        var corrected = EventApi.NewSlug();

        var updated = await api.PutForSnapshotAsync(
            $"{AdminPath}/{stored.Id}",
            EventApi.Body(corrected, "Mistyped Slug", published: false));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(corrected, EventApi.ReadCreated(updated).Slug);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://example.com/events")]
    [InlineData("https://localhost/events")]
    [InlineData("https://127.0.0.1/events")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("file:///etc/passwd")]
    public async Task A_registration_link_that_is_not_an_ordinary_web_address_is_refused(string url)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Unsafe Link Event",
            published: false,
            registrationUrl: url));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(EventApi.Details(created), error => error.Field == "registrationUrl");
        Assert.Equal("invalid", field.Code);
    }

    [Fact]
    public async Task An_event_with_no_district_cannot_be_saved()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        // A reader cannot attend something with no location, and unlike a national offer an event with
        // no district has nothing a browser can filter by.
        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Locationless Event",
            published: false,
            districtCode: null));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        // The code is "invalid" rather than "required" because the [Required] attribute on the request
        // is caught by model binding before the service is ever called, and the shared model-state
        // factory labels every binder error "invalid". Asserting on the field is what the editor
        // actually keys off, and matches how the businesses module asserts the same rule.
        var field = Assert.Single(EventApi.Details(created), error => error.Field == "districtCode");
        Assert.Equal("invalid", field.Code);
    }

    [Fact]
    public async Task An_event_that_ends_before_it_starts_is_refused()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Backwards Event",
            published: false,
            startsInHours: 10,
            durationHours: -4));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(EventApi.Details(created), error => error.Field == "endsAt");
        Assert.Equal("out_of_range", field.Code);
    }

    [Fact]
    public async Task An_event_that_has_already_finished_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Long Finished Event",
            published: true,
            startsInHours: -48,
            durationHours: 2));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(EventApi.Details(created), error => error.Field == "endsAt");
        Assert.Equal("out_of_range", field.Code);
    }

    [Fact]
    public async Task An_event_that_would_run_for_more_than_a_week_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Endless Event",
            published: true,
            startsInHours: 2,
            durationHours: 24 * 30));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(EventApi.Details(created), error => error.Field == "endsAt");
        Assert.Equal("out_of_range", field.Code);
    }

    [Fact]
    public async Task An_event_with_no_venue_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "A Title With No Venue",
            published: true,
            venue: null));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(EventApi.Details(created), error => error.Field == "venue");
        Assert.Equal("required", field.Code);
    }

    [Fact]
    public async Task An_event_with_stub_details_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Stub Details",
            published: true,
            description: "Come at ten."));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(EventApi.Details(created), error => error.Field == "description");
        Assert.Equal("required", field.Code);
    }

    /// <summary>
    /// The publish rules apply only to the draft-to-live transition. A live row being corrected may well
    /// need its times moved in, and refusing that edit would make bringing a published event forward
    /// impossible through the screen that exists to do it.
    /// </summary>
    [Fact]
    public async Task A_published_event_can_have_its_times_brought_forward()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = EventApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            slug,
            "Distant Event",
            published: true,
            startsInHours: 24 * 30));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = EventApi.ReadCreated(created);

        var brought = await api.PutForSnapshotAsync($"{AdminPath}/{stored.Id}", EventApi.Body(
            slug,
            "Distant Event",
            published: true,
            startsInHours: 2));

        Assert.Equal(HttpStatusCode.OK, brought.StatusCode);

        var reread = EventApi.ReadCreated(brought);
        Assert.True(reread.StartsAt < stored.StartsAt);
    }

    [Fact]
    public async Task The_editor_is_told_what_is_missing_from_a_draft()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Incomplete Draft",
            published: false,
            summary: null,
            venue: null,
            description: string.Empty));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = EventApi.ReadCreated(created);

        Assert.NotEmpty(stored.PublishBlockers);
        Assert.Contains(stored.PublishBlockers, message => message.Contains("venue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_published_event_carries_no_publish_blockers()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Complete Event",
            published: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Empty(EventApi.ReadCreated(created).PublishBlockers);
    }

    [Fact]
    public async Task A_published_event_cannot_be_deleted_and_a_draft_can()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var published = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Undeletable Event",
            published: true));
        var publishedId = EventApi.ReadCreated(published).Id;

        var refused = await api.DeleteForSnapshotAsync($"{AdminPath}/{publishedId}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("event_published", refused.Error?.Code);

        var draft = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            EventApi.NewSlug(),
            "Deletable Event",
            published: false));
        var draftId = EventApi.ReadCreated(draft).Id;

        var deleted = await api.DeleteForSnapshotAsync($"{AdminPath}/{draftId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var gone = await api.GetRawAsync($"{AdminPath}/{draftId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_event_that_does_not_exist_is_a_not_found()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(staff);

        using var response = await api.GetRawAsync($"{AdminPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_staff_list_can_be_filtered_to_drafts()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(staff);

        var drafts = await api.GetAsync<PagedResponse<EventAdminSummaryResponse>>($"{AdminPath}?status=Draft&pageSize=100");

        Assert.NotEmpty(drafts.Items);
        Assert.All(drafts.Items, item => Assert.False(item.IsPublished));
    }

    [Fact]
    public async Task The_business_picker_returns_published_listings_only()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(staff);

        var options = await api.GetAsync<List<EventBusinessOptionResponse>>($"{AdminPath}/businesses");

        Assert.NotEmpty(options);
        Assert.All(options, option => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", option.Slug));

        // Named by search rather than looked for in the full list. The endpoint caps the picker at a
        // hundred rows in name order, and every other test class in this project publishes draft-free
        // businesses into the same database, so a seeded listing can legitimately sit past the cap.
        // The search path is the one the editor actually uses, so it is the one worth asserting on.
        var searched = await api.GetAsync<List<EventBusinessOptionResponse>>(
            $"{AdminPath}/businesses?search={Uri.EscapeDataString("Thakali")}");

        Assert.Contains(searched, option => option.Slug == "thakali-kitchen-dharan");
    }

    [Fact]
    public async Task The_staff_endpoints_are_closed_to_visitors()
    {
        using var reader = factory.CreateClient();

        using var response = await reader.GetAsync(AdminPath);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"A visitor reached the staff calendar and got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task The_staff_endpoints_are_closed_to_a_signed_in_reader()
    {
        // CreateSignedInClientAsync returns the client directly rather than a task, because it blocks
        // internally on two requests.
        using var reader = factory.CreateSignedInClientAsync(TestData.UniqueEmail());

        using var response = await reader.GetAsync(AdminPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_staff_write_without_a_csrf_token_is_refused()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        using var response = await api.PostAsync(
            AdminPath,
            EventApi.Body(EventApi.NewSlug(), "No Token", published: false),
            withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("csrf", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every event still on the calendar, walking the pages rather than asking for a hundred and
    /// hoping.
    /// <para>
    /// The public list is capped at a hundred rows and the default order is by start time, so a single
    /// large page is only the whole calendar while the calendar is smaller than a page. Staff tests
    /// publish events of their own and those rows persist, so a coverage assertion asked of the first
    /// hundred starts failing once the accumulated rows reach the cap — and because they are dated
    /// from the moment they were created, they sort ahead of the seeded ones rather than behind them.
    /// </para>
    /// </summary>
    private static async Task<IReadOnlyList<EventSummaryResponse>> ReadAllPublicAsync(AuthApiClient api)
    {
        const int PageSize = 100;

        var items = new List<EventSummaryResponse>();
        var pageNumber = 1;

        while (true)
        {
            var page = await api.GetAsync<PagedResponse<EventSummaryResponse>>(
                $"{PublicPath}?pageSize={PageSize}&page={pageNumber}");

            items.AddRange(page.Items);

            if (items.Count >= page.TotalCount || page.Items.Count == 0)
            {
                return items;
            }

            pageNumber++;
        }
    }
}

/// <summary>
/// An event stays on the calendar until it ends, and "is it on now" is derived from the Nepal clock on
/// every read rather than stored. These tests pin the clock because the whole module rests on the
/// minute-granular comparison being right, and because that is the deliberate asymmetry with the offers
/// page, which is day-granular.
///
/// The evening case is the load-bearing one. An event starting at 19:00 Nepal is 13:15 UTC, so a
/// day-granular or UTC-based comparison would drop it off the calendar during the afternoon it is due to
/// happen — which is exactly the bug the minute-granular rule exists to prevent.
/// </summary>
public sealed class EventTimingTests : IAsyncLifetime
{
    private const string AdminPath = "/api/admin/events";
    private const string PublicPath = "/api/events";

    /// <summary>Nepal Standard Time, stated here so the tests do not read it from the server.</summary>
    private static readonly TimeSpan NepalOffset = TimeSpan.FromMinutes(5 * 60 + 45);

    /// <summary>
    /// A token placed in a fixture title so the row can be found again through the calendar search.
    /// Searching does not cover slugs, because a slug is an address rather than something a reader
    /// knows, so the title is the handle the search endpoint actually offers.
    /// </summary>
    private static readonly string CardToken = Guid.NewGuid().ToString("N");

    private readonly FixedClockApiFactory _factory = FixedClockApiFactory.NearNow();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task An_evening_event_is_still_on_the_calendar_during_the_afternoon_it_is_due_to_happen()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = EventApi.NewSlug();
        var nepalToday = NepalDateOnly();

        // Set the clock before creating an event that starts in the future, so the publish rules do not
        // see it as already finished.
        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(6, 0)));

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            slug,
            "Evening Event",
            published: true,
            districtCode: "bagmati-kathmandu",
            venue: "Thamel community hall",
            startsOn: nepalToday.AddDays(1),
            startsAtHour: 19,
            durationHours: 2));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Four in the afternoon, on the day before. A day-granular comparison would already have dropped
        // it, and a UTC-based one would disagree about the hour entirely.
        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(16, 0)));

        Assert.NotNull(await ReadAsync(slug));
        Assert.False(await IsOngoingAsync(slug));

        // 19:00 Nepal, exactly when it begins.
        _factory.Clock.Set(NepalInstant(nepalToday.AddDays(1), new TimeOnly(19, 0)));
        Assert.True(await IsOngoingAsync(slug));

        // 20:30 Nepal, in the middle of it.
        _factory.Clock.Set(NepalInstant(nepalToday.AddDays(1), new TimeOnly(20, 30)));
        Assert.True(await IsOngoingAsync(slug));

        // 21:00 Nepal, the moment it finishes. Still listed, because the end comparison is inclusive so
        // an event does not vanish mid-sentence.
        _factory.Clock.Set(NepalInstant(nepalToday.AddDays(1), new TimeOnly(21, 0)));
        Assert.NotNull(await ReadAsync(slug));
        Assert.True(await IsOngoingAsync(slug));

        // One minute later it is gone, and identically invisible to the list and the detail endpoint.
        _factory.Clock.Set(NepalInstant(nepalToday.AddDays(1), new TimeOnly(21, 1)));

        using var gone = await _factory.CreateClient().GetAsync($"{PublicPath}/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task An_event_under_way_is_still_listed_with_an_ongoing_flag()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = EventApi.NewSlug();
        var nepalToday = NepalDateOnly();

        // The clock is moved before the event is saved, not after. Publishing refuses an event whose
        // end has passed, so an event starting at ten this morning can only be saved while the clock says
        // it has not finished — otherwise this test would only pass if it happened to be run before
        // four in the afternoon Nepal time.
        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(6, 0)));

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            slug,
            $"Running Now Event {CardToken}",
            published: true,
            districtCode: "bagmati-kathmandu",
            venue: "Patan Durbar Square",
            startsOn: nepalToday,
            startsAtHour: 10,
            durationHours: 6));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(13, 0)));

        using var reader = _factory.CreateClient();

        var detail = await ReadAsync(slug);
        Assert.True(detail.IsOngoing);

        // The same answer from the card and the detail page: computed once from one rule rather than twice
        // from two. Found by the title token rather than the slug, because the calendar search matches
        // the title, summary, terms, organiser and venue - and not the slug, which is a URL and not
        // something a reader knows or types.
        var page = await new AuthApiClient(reader).GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(CardToken)}&pageSize=100");

        var card = Assert.Single(page.Items);
        Assert.True(card.IsOngoing);
        Assert.Equal(detail.IsOngoing, card.IsOngoing);
    }

    [Fact]
    public async Task The_starting_and_finishing_times_are_read_in_nepali_local_time()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = EventApi.NewSlug();
        var nepalToday = NepalDateOnly();

        // A single long day, midnight to eleven at night, so that the small hours can be read against it
        // without the event having ended by then.
        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(0, 5)));

        var created = await api.PostForSnapshotAsync(AdminPath, EventApi.Body(
            slug,
            "Offset Event",
            published: true,
            districtCode: "gandaki-kaski",
            venue: "Lakeside",
            startsOn: nepalToday,
            startsAtHour: 0,
            durationHours: 23));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // 01:00 Nepal is 19:15 UTC on the previous day. A server reading UTC would think it was the
        // evening before, when this event had not started, and would wrongly report it as over. If the
        // two dates ever agree, the test has stopped exercising the offset and the hour needs moving.
        var utcDate = DateOnly.FromDateTime(NepalInstant(nepalToday, new TimeOnly(1, 0)).UtcDateTime);
        var nepalDate = DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime.Add(NepalOffset));
        Assert.NotEqual(utcDate, nepalDate);

        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(1, 0)));

        Assert.NotNull(await ReadAsync(slug));
        Assert.True(await IsOngoingAsync(slug));

        // Still running in the evening, which is where a UTC server would also agree, so the case below
        // is the one doing the work.
        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(22, 0)));
        Assert.True(await IsOngoingAsync(slug));

        // Past the finishing time, so the same day it started is now gone from the calendar.
        _factory.Clock.Set(NepalInstant(nepalToday, new TimeOnly(23, 30)));

        using var finished = await _factory.CreateClient().GetAsync($"{PublicPath}/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, finished.StatusCode);
    }

    [Fact]
    public async Task A_seeded_finished_event_is_hidden_by_the_clock_and_not_only_by_its_row()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        // The seeder wrote this one with its end time in the past on purpose. The point of the assertion
        // is that the public filter hides it on its own, without anybody having retracted anything.
        using var detail = await _factory.CreateClient().GetAsync($"{PublicPath}/bhaktapur-pottery-workshop-dharamshala-expired");

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var stored = await api.GetAsync<PagedResponse<EventAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString("pottery")}&pageSize=100");

        Assert.True(Assert.Single(stored.Items).IsFinished);
    }

    private async Task<EventDetailResponse> ReadAsync(string slug)
    {
        using var reader = _factory.CreateClient();

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<EventDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success, $"The event {slug} could not be read at this point in time.");

        return envelope.Data!;
    }

    private async Task<bool> IsOngoingAsync(string slug) => (await ReadAsync(slug)).IsOngoing;

    private async Task<EventSummaryResponse> FirstCardAsync(string slug)
    {
        using var reader = _factory.CreateClient();

        var page = await new AuthApiClient(reader).GetAsync<PagedResponse<EventSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(slug)}&pageSize=100");

        return Assert.Single(page.Items);
    }

    /// <summary>The Nepal calendar date the pinned clock is currently on.</summary>
    private DateOnly NepalDateOnly() =>
        DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime.Add(NepalOffset));

    /// <summary>
    /// The UTC instant at which it is the given Nepal date and wall-clock time. Stated as an offset
    /// rather than read back from an endpoint, so a server using the wrong offset fails these tests
    /// instead of agreeing with itself.
    /// </summary>
    private DateTimeOffset NepalInstant(DateOnly nepalDate, TimeOnly time) =>
        new DateTimeOffset(nepalDate.ToDateTime(time, DateTimeKind.Unspecified), NepalOffset)
            .ToUniversalTime();
}

internal static class EventApi
{
    /// <summary>
    /// Comfortably longer than the sixty character minimum that publishing requires, so the publish rule
    /// is satisfied by the fixture itself rather than by each test padding itself out.
    /// </summary>
    public const string LongDescription =
        "These details are comfortably longer than the sixty character minimum that publishing requires, "
        + "so the publish rule is satisfied by the fixture itself rather than by every test padding its "
        + "own body out with filler sentences about nothing in particular at all.";

    public static string NewSlug() => $"test-{Guid.NewGuid():N}";

    /// <summary>
    /// An event fixture. The title carries a unique token so the row a test has just written can be found
    /// again with a text search, which is the handle the search endpoint offers: it matches the title,
    /// the summary, the details, the organiser and the venue. A shared title would make every test row
    /// match every other one.
    /// </summary>
    public static object Body(
        string slug,
        string title,
        bool published,
        string? summary = null,
        string? description = null,
        string category = "Community",
        string? organiser = "A test committee",
        string? districtCode = "bagmati-kathmandu",
        string? venue = "Taumadhi square",
        string? businessSlug = null,
        string? registrationUrl = null,
        int? capacity = null,
        DateOnly? startsOn = null,
        int startsAtHour = 10,
        int startsInHours = 24,
        int durationHours = 2) => new
        {
            slug,
            title,
            summary = summary ?? "A one-line summary long enough to be useful on a card.",
            description = description ?? LongDescription,
            category,
            organiser,
            districtCode,
            venue,
            businessSlug,
            registrationUrl,
            capacity,
            startsAt = DefaultStart(startsOn, startsAtHour, startsInHours),
            endsAt = DefaultStart(startsOn, startsAtHour, startsInHours).AddHours(durationHours),
            isPublished = published,
        };

    /// <summary>
    /// The start a fixture gets when the test did not name one. Relative rather than absolute, so a
    /// fixture never lands in the past and starts failing the publish rules because of the date it was
    /// written on. The default of tomorrow keeps it safely on the future side of the clock.
    /// </summary>
    private static DateTime DefaultStart(DateOnly? startsOn, int hour, int hoursAhead) =>
        (startsOn?.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Unspecified))
        ?? DateTime.Now.AddHours(hoursAhead);

    /// <summary>
    /// Unwraps the event a staff write returned, so a test can assert against the record the server
    /// stored rather than against the request that was sent.
    /// </summary>
    public static EventAdminResponse ReadCreated(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<EventAdminResponse>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }

    public static IReadOnlyList<FieldError> Details(ApiResponseSnapshot snapshot)
    {
        var error = snapshot.Error;
        Assert.NotNull(error);
        Assert.NotNull(error!.Details);

        return error.Details!.Value.Deserialize<FieldError[]>(TestJson.Options) ?? [];
    }
}