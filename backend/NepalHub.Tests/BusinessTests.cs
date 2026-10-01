using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Businesses;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Reference;

namespace NepalHub.Tests;

/// <summary>
/// Reading the directory as a visitor. Nothing here signs in, which is the point: a local services
/// directory has to work for somebody who has never made an account.
///
/// Assertions are scoped to a search term or a seed slug wherever a count is involved, rather than
/// asserting a whole-directory total, because the staff tests in this file publish rows of their
/// own and a suite that only passed because of the order its classes ran in would fail the next time
/// somebody reordered it.
/// </summary>
public sealed class BusinessDirectoryTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string PublicPath = "/api/businesses";
    private const string AdminPath = "/api/admin/businesses";

    [Fact]
    public async Task The_public_directory_is_readable_without_an_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_seeded_directory_covers_every_category_and_every_province()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var items = await ReadAllAsync(api);

// Only seeded listings count for this assertion. Tests in this file create additional listings,
// so counting the whole directory would make the assertion fail as soon as any test ran before
// or after it.
var seededSlugs = new HashSet<string>(
    new[]
    {
        "newa-momo-house-lalitpur",
        "bhojan-ghar-patan",
        "thakali-kitchen-dharan",
        "sajha-coffee-lalitpur",
        "himalayan-cafe-pokhara",
        "shivapuri-lodge-nagarkot",
        "janaki-guest-house-bharatpur",
        "himal-gym-thamel",
        "sajilo-hair-salon-baneshwor",
        "gorkha-tailoring-biratnagar",
        "kathmandu-tuition-centre-thapathali",
        "everest-english-school-pokhara",
        "janaki-school-bardibas",
        "valley-motor-works-patan",
        "himalayan-craft-repair-bhaktapur",
        "himal-handicraft-emporium-bhaktapur",
        "everest-hardware-dhangadhi",
        "machhapuchhre-books-kathmandu",
        "sankhu-tech-support-baneshwor",
        "everest-web-studio-lalitpur",
        "sagar-associates-kathmandu",
        "lumbini-legal-consultancy-butwal",
        "karnali-highland-consultancy-birendranagar",
        "kathmandu-dental-care-thapathali",
        "everest-pharmacy-biratnagar",
        "everest-craft-house-pokhara",
    },
    StringComparer.Ordinal);

var seeded = items.Where(item => seededSlugs.Contains(item.Slug)).ToList();

        // A category nobody seeded is a category whose filter silently returns nothing, which a
        // visitor reports as "your search is broken" rather than as an empty category.
        foreach (var category in Enum.GetValues<BusinessCategory>())
        {
            Assert.Contains(seeded, item => item.Category == category);
        }

        // The province is read through the seeded district, so this asks the reference endpoints
        // which province each seeded district is in rather than asserting a list the test supplied
        // to itself.
        var provinces = await api.GetAsync<List<ProvinceResponse>>("/api/reference/provinces");
        var districtsIn = new Dictionary<string, List<DistrictResponse>>(StringComparer.Ordinal);

        foreach (var province in provinces)
        {
            districtsIn[province.Name] = await api.GetAsync<List<DistrictResponse>>(
                $"/api/reference/provinces/{province.Code}/districts");
        }

        var seededDistricts = seeded
            .Select(item => item.DistrictName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var province in provinces)
        {
            var hasListing = districtsIn[province.Name]
                .Any(district => seededDistricts.Contains(district.Name));

            Assert.True(hasListing, $"The seeded directory has no listing in {province.Name}.");
        }
    }

    [Fact]
    public async Task A_seeded_listing_opens_and_reports_its_district_and_province()
    {
        using var client = factory.CreateClient();

        var envelope = await client.GetFromJsonAsync<ApiEnvelope<BusinessDetailResponse>>(
            $"{PublicPath}/newa-momo-house-lalitpur",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal("Newa Momo House", envelope!.Data!.Name);
        Assert.Equal(BusinessCategory.Restaurants, envelope.Data.Category);
        Assert.Equal("Lalitpur", envelope.Data.DistrictName);
        Assert.Equal("Bagmati", envelope.Data.ProvinceName);
        Assert.True(envelope.Data.IsVerified);
    }

    [Fact]
    public async Task A_draft_is_invisible_to_readers_and_answers_the_same_404_as_a_slug_that_never_existed()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var created = await staffApi.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), $"Hidden Corner {Guid.NewGuid():N}", published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var slug = BusinessApi.ReadCreated(created).Slug;

        var publicPage = await new AuthApiClient(reader).GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(slug)}");
        Assert.Equal(0, publicPage.TotalCount);

        using var draft = await reader.GetAsync($"{PublicPath}/{slug}");
        using var invented = await reader.GetAsync($"{PublicPath}/no-such-listing-{Guid.NewGuid():N}");

        // The same answer for both, so the endpoint cannot be used to find out which slugs are real
        // but not yet public.
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invented.StatusCode);
    }

    [Fact]
    public async Task A_published_listing_reaches_readers_with_the_fields_a_visitor_acts_on()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var name = $"Reachable Kitchen {Guid.NewGuid():N}";
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            name,
            published: true,
            isVerified: true,
            phone: "9800000001",
            districtCode: "bagmati-bhaktapur",
            address: "Taumadhi square"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<BusinessDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal(name, envelope!.Data!.Name);
        Assert.Equal("Bhaktapur", envelope.Data.DistrictName);
        Assert.Equal("Bagmati", envelope.Data.ProvinceName);
        Assert.Equal("Taumadhi square", envelope.Data.Address);
        Assert.Equal("9800000001", envelope.Data.Phone);
    }

    [Fact]
    public async Task A_published_listing_with_no_hours_reports_unknown_rather_than_closed()
    {
        using var client = factory.CreateClient();

        var envelope = await client.GetFromJsonAsync<ApiEnvelope<BusinessDetailResponse>>(
            $"{PublicPath}/everest-hardware-dhangadhi",
            TestJson.Options);

        Assert.NotNull(envelope);

        // "Unknown" and "closed" are different statements, and a red closed badge on a shop that
        // certainly opens is telling the reader something untrue.
        Assert.Null(envelope!.Data!.IsOpenNow);
        Assert.Empty(envelope.Data.OpeningHours);
    }

    [Fact]
    public async Task A_week_of_hours_comes_back_as_seven_days_with_the_unlisted_ones_closed()
    {
        using var client = factory.CreateClient();

        var envelope = await client.GetFromJsonAsync<ApiEnvelope<BusinessDetailResponse>>(
            $"{PublicPath}/janaki-school-bardibas",
            TestJson.Options);

        Assert.NotNull(envelope);
        var hours = envelope!.Data!.OpeningHours;

        // A reader wants the whole week at a glance, so a missing day reads as closed.
        Assert.Equal(7, hours.Count);
        Assert.Equal(DayOfWeek.Sunday, hours[0].DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, hours[6].DayOfWeek);

        var saturday = Assert.Single(hours, day => day.DayOfWeek == DayOfWeek.Saturday);
        Assert.True(saturday.IsClosed);
        Assert.Null(saturday.OpensAt);
        Assert.Null(saturday.ClosesAt);

        Assert.All(
            hours.Where(day => day.DayOfWeek != DayOfWeek.Saturday),
            day =>
            {
                Assert.False(day.IsClosed);
                Assert.NotNull(day.OpensAt);
                Assert.NotNull(day.ClosesAt);
            });
    }

    [Fact]
    public async Task A_card_carries_a_place_and_a_contact_but_not_the_whole_description()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var items = await ReadAllAsync(api);
        var card = Assert.Single(items, item => item.Slug == "newa-momo-house-lalitpur");

        Assert.Equal("Lalitpur", card.DistrictName);
        Assert.Equal("Bagmati", card.ProvinceName);
        Assert.False(string.IsNullOrWhiteSpace(card.Phone));

        // The description is on the detail endpoint. Carrying it on every row would make the list
        // several times larger for content the reader has not asked for.
        Assert.DoesNotContain(
            "description",
            JsonSerializer.Serialize(card, TestJson.Options),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_search_matches_the_name_the_description_and_the_address()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // The search matches the text of the row, not its slug, so the term has to be something the
        // listing actually says. "Himal Gym" is the start of its name; "himal-gym" would not match,
        // because the slug is not a searchable field.
        var byName = await ReadAllAsync(api, "search=Himal%20Gym");
        Assert.Contains(byName, item => item.Slug == "himal-gym-thamel");

        // The slug itself is deliberately not a search field.
        var bySlug = await ReadAllAsync(api, "search=himal-gym-thamel");
        Assert.DoesNotContain(bySlug, item => item.Slug == "himal-gym-thamel");

        // "Thamel" is in the gym's address rather than its name or description, which is the case a
        // naive title-only search would miss.
        var byAddress = await ReadAllAsync(api, "search=Thamel");
        Assert.Contains(byAddress, item => item.Slug == "himal-gym-thamel");

        var byDescription = await ReadAllAsync(api, "search=momo");
        Assert.Contains(byDescription, item => item.Slug == "newa-momo-house-lalitpur");
    }

    [Fact]
    public async Task The_search_does_not_match_a_phone_number()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // 9843456789 belongs to exactly one seeded listing. A keyword search that reached contact
        // details would let someone enumerate the phone numbers of small businesses.
        var found = await ReadAllAsync(api, "search=9843456789");

        Assert.DoesNotContain(found, item => item.Phone == "9843456789");
    }

    [Fact]
    public async Task A_search_term_containing_a_wildcard_is_matched_literally()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // "%" is the LIKE wildcard. Passed through unescaped it would match every row in the
        // directory, which is both wrong and a way to pull the whole table through one request.
        var found = await ReadAllAsync(api, $"search={Uri.EscapeDataString("%")}");

        Assert.Empty(found);
    }

    [Theory]
    [InlineData("category=Nonsense", "category_invalid")]
    [InlineData("sort=Oldest", "sort_invalid")]
    [InlineData("verified=maybe", "verified_invalid")]
    [InlineData("province=atlantis", "province_invalid")]
    [InlineData("district=not-a-district", "district_invalid")]
    public async Task An_unrecognised_public_filter_is_refused_with_a_named_error(string query, string expectedCode)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?{query}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, new ApiResponseSnapshot(response.StatusCode, json).Error?.Code);
    }

    [Fact]
    public async Task A_blank_filter_means_no_filter_rather_than_an_error()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?category=&search=&verified=&sort=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_category_filter_returns_only_that_category()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var cafes = await ReadAllAsync(api, "category=Cafes");

        Assert.NotEmpty(cafes);
        Assert.All(cafes, item => Assert.Equal(BusinessCategory.Cafes, item.Category));
    }

    [Fact]
    public async Task The_verified_filter_separates_checked_listings_from_unchecked_ones()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var verified = await ReadAllAsync(api, "verified=true");
        var unverified = await ReadAllAsync(api, "verified=false");

        Assert.NotEmpty(verified);
        Assert.All(verified, item => Assert.True(item.IsVerified));

        // The seeded directory ships one listing that is live but no longer carries its badge,
        // which is the state unverifying creates and the reason the two flags are independent.
        Assert.NotEmpty(unverified);
        Assert.All(unverified, item => Assert.False(item.IsVerified));
        Assert.Contains(unverified, item => item.Slug == "kathmandu-dental-care-thapathali");
    }

    [Fact]
    public async Task The_name_sort_starts_at_the_top_of_the_alphabet()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?sort=Name&pageSize=100");

        var names = page.Items.Select(item => item.Name).ToList();

        Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal), names);
    }

    [Fact]
    public async Task The_newest_sort_puts_the_most_recently_created_listing_first()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var olderName = $"Older Corner {Guid.NewGuid():N}";
        var newerName = $"Newer Corner {Guid.NewGuid():N}";

        Assert.Equal(
            HttpStatusCode.Created,
            (await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
                BusinessApi.NewSlug(),
                olderName,
                published: true,
                isVerified: true,
                phone: "9800000002"))).StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            (await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
                BusinessApi.NewSlug(),
                newerName,
                published: true,
                isVerified: true,
                phone: "9800000003"))).StatusCode);

        var page = await new AuthApiClient(reader).GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?sort=Newest&pageSize=100");

        var names = page.Items.Select(item => item.Name).ToList();
        var newerIndex = names.FindIndex(name => name == newerName);
        var olderIndex = names.FindIndex(name => name == olderName);

        Assert.True(newerIndex >= 0, "The newer listing is missing from the directory.");
        Assert.True(olderIndex >= 0, "The older listing is missing from the directory.");
        Assert.True(newerIndex < olderIndex, "The newest sort did not put the newer listing first.");
    }

    [Fact]
    public async Task The_directory_pages_without_repeating_or_dropping_a_listing()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var first = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>($"{PublicPath}?sort=Name&pageSize=5");
        var second = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>($"{PublicPath}?sort=Name&pageSize=5&page=2");

        var firstSlugs = first.Items.Select(item => item.Slug).ToList();
        var secondSlugs = second.Items.Select(item => item.Slug).ToList();

        Assert.Equal(5, firstSlugs.Count);
        Assert.Equal(5, secondSlugs.Count);
        Assert.Empty(firstSlugs.Intersect(secondSlugs, StringComparer.Ordinal));
        Assert.True(second.TotalCount >= first.TotalCount);
    }

    /// <summary>
    /// Every matching row, walking the pages rather than asking for a hundred and hoping.
    /// The test database is shared and is never emptied between runs, so a single large page is
    /// not a reliable way to see the whole directory: rows fall off the end and a coverage
    /// assertion then fails for a reason that has nothing to do with the seed data.
    /// </summary>
    private static async Task<IReadOnlyList<BusinessSummaryResponse>> ReadAllAsync(
        AuthApiClient api,
        string? query = null)
    {
        const int PageSize = 100;

        var items = new List<BusinessSummaryResponse>();
        var pageNumber = 1;

        while (true)
        {
            var page = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>(
                $"{PublicPath}?pageSize={PageSize}&page={pageNumber}{(query is null ? string.Empty : $"&{query}")}");

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
/// Staff management of the directory: who may write, what a draft is allowed to be missing, and what
/// happens to a listing that is already public.
/// </summary>
public sealed class BusinessAdminTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string AdminPath = "/api/admin/businesses";
    private const string PublicPath = "/api/businesses";

    [Fact]
    public async Task The_staff_endpoints_are_closed_to_anonymous_visitors()
    {
        using var client = factory.CreateClient();

        using var listed = await client.GetAsync(AdminPath);
        using var fetched = await client.GetAsync($"{AdminPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, fetched.StatusCode);
    }

    [Fact]
    public async Task The_staff_endpoints_are_closed_to_a_standard_account()
    {
        using var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail());
        var api = new AuthApiClient(client);

        using var listed = await client.GetAsync(AdminPath);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "A standard account should not be able to write",
            published: false));

        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    [Fact]
    public async Task A_staff_write_without_a_csrf_token_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        using var response = await api.PostAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), "A staff account that skipped the token", published: false),
            withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_incomplete_draft_can_be_saved_because_a_draft_is_not_junk()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug = BusinessApi.NewSlug(),
            name = "A listing that is still being written",
            description = string.Empty,
            category = "Other",
            districtCode = "bagmati-lalitpur",
            address = (string?)null,
            phone = (string?)null,
            email = (string?)null,
            website = (string?)null,
            isPublished = false,
            isVerified = false,
            openingHours = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The shape rules passed, and the editor is told exactly what is still missing.
        var record = BusinessApi.ReadCreated(created);
        Assert.False(record.IsPublished);
        Assert.Contains(record.PublishBlockers, blocker => blocker.Contains("phone number"));
        Assert.Contains(record.PublishBlockers, blocker => blocker.Contains("60 characters"));
        Assert.Contains(record.PublishBlockers, blocker => blocker.Contains("Verify this listing"));
    }

    [Fact]
    public async Task A_draft_is_visible_to_staff_and_hidden_from_readers()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();
        var name = $"Draft Corner {Guid.NewGuid():N}";

        var created = await staffApi.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), name, published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var staffPage = await staffApi.GetAsync<PagedResponse<BusinessAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString(name)}");
        var row = Assert.Single(staffPage.Items);

        Assert.Equal(BusinessApi.ReadCreated(created).Slug, row.Slug);
        Assert.False(row.IsPublished);

        var publicPage = await new AuthApiClient(reader).GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(name)}");
        Assert.Equal(0, publicPage.TotalCount);
    }

    [Fact]
    public async Task A_duplicate_slug_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();

        var first = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(slug, "First Corner", published: false));
        var second = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(slug, "Second Corner", published: false));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("slug_taken", second.Error?.Code);
    }

    [Fact]
    public async Task Renaming_a_draft_onto_a_slug_another_listing_already_owns_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var taken = BusinessApi.NewSlug();
        var mine = BusinessApi.NewSlug();

        Assert.Equal(
            HttpStatusCode.Created,
            (await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(taken, "Slug Owner", published: false))).StatusCode);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(mine, "Slug Claimant", published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var renamed = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(taken, "Slug Claimant", published: false));

        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
        Assert.Equal("slug_taken", renamed.Error?.Code);
    }

    [Theory]
    [InlineData("Not A Slug")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("UPPER")]
    public async Task A_malformed_slug_is_refused_with_a_field_error(string slug)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(slug, "Some Corner", published: false));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "slug");
    }

    [Fact]
    public async Task A_name_that_is_too_short_is_refused_with_a_field_error()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), "a", published: false));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "name");
    }

    /// <summary>
    /// A draft keeps an editable slug, because a staff member who mistyped it has not yet sent
    /// anyone to the wrong address.
    /// </summary>
    [Fact]
    public async Task A_drafts_slug_can_be_corrected()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            $"mistyped-{Guid.NewGuid():N}",
            "Some Corner",
            published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var corrected = BusinessApi.NewSlug();
        var renamed = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(corrected, "Some Corner", published: false));

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(corrected, BusinessApi.ReadCreated(renamed).Slug);
    }

    /// <summary>
    /// Once published, the slug is the address people are sharing, so it freezes. A posting is
    /// written and edited before anyone has the link, so freezing from creation would make every
    /// typo permanent for no reader's benefit.
    /// </summary>
    [Fact]
    public async Task A_published_listings_slug_cannot_be_changed()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();
        var name = $"Frozen Address {Guid.NewGuid():N}";

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            name,
            published: true,
            isVerified: true,
            phone: "9800000004"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var renamed = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(BusinessApi.NewSlug(), name, published: true, isVerified: true, phone: "9800000004"));

        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
        Assert.Equal("slug_immutable", renamed.Error?.Code);
    }

    [Fact]
    public async Task A_published_listings_other_fields_are_still_editable()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Original Corner",
            published: true,
            isVerified: true,
            phone: "9800000005"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var updated = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(slug, "Corrected Corner", published: true, isVerified: true, phone: "9800000005"));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Corrected Corner", BusinessApi.ReadCreated(updated).Name);
    }

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com")]
    public async Task An_unsafe_website_is_refused(string url)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), "Some Corner", published: false, website: url));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "website");
    }

    [Theory]
    [InlineData("nepalhub.test")]
    [InlineData("nepalhub@localhost")]
    [InlineData("@nepalhub.test")]
    [InlineData("nepalhub@.test")]
    public async Task An_email_that_is_not_shaped_like_an_email_is_refused(string email)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), "Some Corner", published: false, email: email));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "email");
    }

    [Fact]
    public async Task An_unknown_district_code_is_refused_rather_than_silently_dropping_the_location()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        // A directory entry that says it is in Kathmandu and is actually nowhere in particular is
        // the worst outcome for somebody browsing their own district, so the code is checked.
        var created = await api.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), "Some Corner", published: false, districtCode: "not-a-real-district"));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "districtCode");
    }

    [Fact]
    public async Task A_listing_with_no_district_is_refused_even_as_a_draft()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        // A remote job legitimately has no location. A local business does not: a listing with no
        // district cannot be found by anyone browsing their own area.
        var created = await api.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), "Nowhere In Particular", published: false, districtCode: null));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "districtCode");
    }

    [Fact]
    public async Task A_district_code_is_stored_and_its_province_is_read_through_it()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), "Some Corner", published: false, districtCode: "gandaki-kaski"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = BusinessApi.ReadCreated(created);
        Assert.Equal("gandaki-kaski", record.DistrictCode);
        Assert.Equal("Kaski", record.DistrictName);
        Assert.Equal("gandaki", record.ProvinceCode);
        Assert.Equal("Gandaki", record.ProvinceName);
    }

    [Fact]
    public async Task Publishing_an_unverified_listing_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        // The badge and the visibility are two columns, so publishing can be checked against the
        // flag rather than inferred from it.
        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Unchecked Corner",
            published: true,
            isVerified: false,
            phone: "9800000006"));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "isVerified");
    }

    [Fact]
    public async Task Publishing_a_listing_with_no_phone_number_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Unreachable Corner",
            published: true,
            isVerified: true,
            phone: null));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "phone");
    }

    [Fact]
    public async Task Publishing_a_listing_with_too_short_a_description_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Stub Corner",
            published: true,
            isVerified: true,
            phone: "9800000007",
            description: "Momos."));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "description");
    }

    [Fact]
    public async Task A_complete_listing_can_be_published_and_then_reaches_readers()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();
        var name = $"Publishable Corner {Guid.NewGuid():N}";
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            name,
            published: true,
            isVerified: true,
            phone: "9800000008",
            districtCode: "bagmati-bhaktapur"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = BusinessApi.ReadCreated(created);
        Assert.True(record.IsPublished);
        Assert.Empty(record.PublishBlockers);
        Assert.NotNull(record.VerifiedAt);

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<BusinessDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal(name, envelope!.Data!.Name);
    }

    [Fact]
    public async Task The_publish_blockers_clear_once_the_draft_is_filled_in()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "A listing that is not finished yet",
            published: false,
            isVerified: false,
            phone: null,
            description: string.Empty));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var draft = BusinessApi.ReadCreated(created);
        Assert.NotEmpty(draft.PublishBlockers);

        var filled = await api.PutForSnapshotAsync(
            $"{AdminPath}/{draft.Id}",
            BusinessApi.Body(slug, "A listing that is not finished yet", published: true, isVerified: true, phone: "9800000009"));

        Assert.Equal(HttpStatusCode.OK, filled.StatusCode);
        Assert.Empty(BusinessApi.ReadCreated(filled).PublishBlockers);
    }

    /// <summary>
    /// Unverifying is not retracting. A live listing whose details turned out to be wrong stays on
    /// the directory without its badge, so a visitor sees it honestly unchecked while somebody takes
    /// responsibility for the correction. Merging the flags would force the listing off the
    /// directory entirely, which is a worse outcome than showing it unchecked.
    /// </summary>
    [Fact]
    public async Task Unverifying_a_published_listing_leaves_it_public()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();
        var name = $"Unverified Corner {Guid.NewGuid():N}";

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            name,
            published: true,
            isVerified: true,
            phone: "9800000010"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var unverified = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(slug, name, published: true, isVerified: false, phone: "9800000010"));

        Assert.Equal(HttpStatusCode.OK, unverified.StatusCode);

        var record = BusinessApi.ReadCreated(unverified);
        Assert.True(record.IsPublished);
        Assert.False(record.IsVerified);
        Assert.Null(record.VerifiedAt);

        using var reader = factory.CreateClient();
        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<BusinessDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        // The listing still answers, and still carries the badge as absent. Reading it at all is the
        // assertion: a retracted one would be a 404 here.
        Assert.NotNull(envelope);
        Assert.False(envelope!.Data!.IsVerified);
    }

    [Fact]
    public async Task A_published_listing_cannot_be_deleted_but_a_draft_can()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Corner To Be Retracted",
            published: true,
            isVerified: true,
            phone: "9800000011"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var published = BusinessApi.ReadCreated(created);
        var refused = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("business_published", refused.Error?.Code);

        // Retract, then delete. Deleting a live row would lose the record of what was public.
        var retracted = await api.PutForSnapshotAsync(
            $"{AdminPath}/{published.Id}",
            BusinessApi.Body(slug, "Corner To Be Retracted", published: false, isVerified: true, phone: "9800000011"));
        Assert.Equal(HttpStatusCode.OK, retracted.StatusCode);

        var deleted = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");
        var missing = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task The_staff_status_filter_separates_drafts_from_published_rows()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var name = $"Status Filter Corner {Guid.NewGuid():N}";
        var slug = BusinessApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(slug, name, published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var drafts = await api.GetAsync<PagedResponse<BusinessAdminSummaryResponse>>(
            $"{AdminPath}?status=Draft&search={Uri.EscapeDataString(name)}");
        Assert.False(Assert.Single(drafts.Items).IsPublished);

        Assert.Equal(
            HttpStatusCode.OK,
            (await api.PutForSnapshotAsync(
                $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
                BusinessApi.Body(slug, name, published: true, isVerified: true, phone: "9800000012"))).StatusCode);

        var published = await api.GetAsync<PagedResponse<BusinessAdminSummaryResponse>>(
            $"{AdminPath}?status=Published&search={Uri.EscapeDataString(name)}");
        Assert.True(Assert.Single(published.Items).IsPublished);
    }

    [Theory]
    [InlineData("status=Wibble", "status_invalid")]
    [InlineData("category=Nonsense", "category_invalid")]
    [InlineData("province=atlantis", "province_invalid")]
    [InlineData("verified=perhaps", "verified_invalid")]
    public async Task An_unrecognised_staff_filter_is_refused_with_a_named_error(string query, string expectedCode)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);

        using var response = await client.GetAsync($"{AdminPath}?{query}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, new ApiResponseSnapshot(response.StatusCode, json).Error?.Code);
    }

    [Fact]
    public async Task Updating_a_listing_that_does_not_exist_is_not_found()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var updated = await api.PutForSnapshotAsync(
            $"{AdminPath}/{Guid.NewGuid()}",
            BusinessApi.Body(BusinessApi.NewSlug(), "Some Corner", published: false));

        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        Assert.Equal("business_not_found", updated.Error?.Code);
    }

    [Fact]
    public async Task Reading_a_listing_that_does_not_exist_is_not_found()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);

        using var response = await client.GetAsync($"{AdminPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_staff_list_reports_the_author_who_wrote_each_listing()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        var name = $"Authored Corner {Guid.NewGuid():N}";
        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(BusinessApi.NewSlug(), name, published: false));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.EndsWith("@nepalhub.test", BusinessApi.ReadCreated(created).CreatedByEmail);

        var page = await api.GetAsync<PagedResponse<BusinessAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString(name)}");

        Assert.EndsWith("@nepalhub.test", Assert.Single(page.Items).CreatedByEmail);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Moderator)]
    public async Task An_admin_and_a_moderator_can_both_write(string role)
    {
        using var client = await factory.CreateStaffClientAsync(role);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            BusinessApi.Body(BusinessApi.NewSlug(), $"Role {role} Corner", published: false));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }
}

/// <summary>
/// Opening hours are the one place in a save where the whole set has to be judged rather than one
/// input at a time, and the only rule that depends on a clock rather than on a stored flag. The
/// clock tests therefore get a host whose clock the test can move.
/// </summary>
public sealed class BusinessOpeningHoursTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string AdminPath = "/api/admin/businesses";
    private const string PublicPath = "/api/businesses";

    [Fact]
    public async Task A_listing_can_be_published_with_no_hours_at_all()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        // "Not listed" is honest where a wrong guess is not, so an empty week is allowed.
        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "No Hours Corner",
            published: false,
            openingHours: []));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Empty(BusinessApi.ReadCreated(created).OpeningHours);
    }

    [Fact]
    public async Task A_full_week_of_hours_round_trips_as_seven_days()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Seven Day Corner",
            published: false,
            openingHours: BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(18, 0))));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var hours = BusinessApi.ReadCreated(created).OpeningHours;
        Assert.Equal(7, hours.Count);
        Assert.All(hours, day =>
        {
            Assert.False(day.IsClosed);
            Assert.Equal(new TimeOnly(9, 0), day.OpensAt);
            Assert.Equal(new TimeOnly(18, 0), day.ClosesAt);
        });
    }

    [Fact]
    public async Task A_day_marked_closed_is_stored_without_times()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Sabbath Corner",
            published: false,
            openingHours:
            [
                BusinessApi.Day(DayOfWeek.Saturday, isClosed: true),
                BusinessApi.Day(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(14, 0)),
            ]));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var saturday = Assert.Single(
            BusinessApi.ReadCreated(created).OpeningHours,
            day => day.DayOfWeek == DayOfWeek.Saturday);

        Assert.True(saturday.IsClosed);
        Assert.Null(saturday.OpensAt);
        Assert.Null(saturday.ClosesAt);
    }

    /// <summary>
    /// A closing time earlier than the opening time means the shift runs past midnight, which is
    /// ordinary for a bar or a bakery. Refusing it would leave the businesses most likely to publish
    /// hours unable to.
    /// </summary>
    [Fact]
    public async Task A_shift_that_runs_past_midnight_is_allowed()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Late Night Bar",
            published: false,
            openingHours: [BusinessApi.Day(DayOfWeek.Friday, new TimeOnly(22, 0), new TimeOnly(2, 0))]));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var friday = Assert.Single(
            BusinessApi.ReadCreated(created).OpeningHours,
            day => day.DayOfWeek == DayOfWeek.Friday);

        Assert.Equal(new TimeOnly(22, 0), friday.OpensAt);
        Assert.Equal(new TimeOnly(2, 0), friday.ClosesAt);
    }

    [Fact]
    public async Task A_day_opened_and_closed_at_the_same_time_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Never Open Corner",
            published: false,
            openingHours: [BusinessApi.Day(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 0))]));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "openingHours");
    }

    [Fact]
    public async Task A_day_that_is_not_closed_needs_both_times()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Half Stated Corner",
            published: false,
            openingHours: [BusinessApi.Day(DayOfWeek.Monday, new TimeOnly(9, 0), null)]));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Field == "openingHours");
    }

    [Fact]
    public async Task The_same_day_listed_twice_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Duplicated Week Corner",
            published: false,
            openingHours:
            [
                BusinessApi.Day(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                BusinessApi.Day(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(20, 0)),
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(BusinessApi.Details(created), error => error.Code == "duplicate");
    }

    [Fact]
    public async Task More_than_seven_days_of_hours_is_refused_once()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            BusinessApi.NewSlug(),
            "Eight Day Week Corner",
            published: false,
            openingHours:
            [
                .. BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0)),
                BusinessApi.Day(DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        // Reported once. Every further complaint would be about rows the first error already rules
        // out, and staff should see one thing to fix.
        var hoursErrors = BusinessApi.Details(created).Where(error => error.Field == "openingHours").ToList();
        Assert.Single(hoursErrors);
    }

    /// <summary>
    /// The whole set is replaced on save. Hours are at most seven rows whose meaning is the schedule
    /// as a whole, so a partial update would only create ways for one day to fall out of step with
    /// the other six. The row is keyed by business and day, so this is also the case where a naive
    /// replace has to deal with two instances sharing one key.
    /// </summary>
    [Fact]
    public async Task Saving_replaces_the_whole_week_rather_than_adding_to_it()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Replaced Week Corner",
            published: false,
            openingHours: BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(7, BusinessApi.ReadCreated(created).OpeningHours.Count);

        var replaced = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(
                slug,
                "Replaced Week Corner",
                published: false,
                openingHours: [BusinessApi.Day(DayOfWeek.Saturday, new TimeOnly(11, 0), new TimeOnly(15, 0))]));

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);

        var hours = BusinessApi.ReadCreated(replaced).OpeningHours;

        // The response reads as a whole week, so a saved Saturday shows six closed days around it.
        Assert.Equal(7, hours.Count);
        var saturday = Assert.Single(hours, day => day.DayOfWeek == DayOfWeek.Saturday);
        Assert.Equal(new TimeOnly(11, 0), saturday.OpensAt);
        Assert.All(
            hours.Where(day => day.DayOfWeek != DayOfWeek.Saturday),
            day => Assert.True(day.IsClosed));
    }

    [Fact]
    public async Task Saving_the_same_week_twice_does_not_double_it()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Idempotent Week Corner",
            published: false,
            openingHours: BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var again = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(
                slug,
                "Idempotent Week Corner",
                published: false,
                openingHours: BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0))));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var reloaded = await api.GetAsync<BusinessAdminResponse>($"{AdminPath}/{BusinessApi.ReadCreated(created).Id}");
        Assert.Equal(7, reloaded.OpeningHours.Count);
    }

    [Fact]
    public async Task Clearing_the_hours_leaves_a_listing_with_none()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = BusinessApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Cleared Week Corner",
            published: false,
            openingHours: BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var cleared = await api.PutForSnapshotAsync(
            $"{AdminPath}/{BusinessApi.ReadCreated(created).Id}",
            BusinessApi.Body(slug, "Cleared Week Corner", published: false, openingHours: []));

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Empty(BusinessApi.ReadCreated(cleared).OpeningHours);
    }
}

/// <summary>
/// "Is it open now" is derived from the hours and the current Nepal time on every read, and stored
/// nowhere: it stops being true at half past nine on its own, and a stored flag would be wrong by
/// lunchtime. These tests pin the clock, including across midnight, because the offset is
/// load-bearing rather than incidental.
/// </summary>
public sealed class BusinessOpenNowTests : IAsyncLifetime
{
    private const string AdminPath = "/api/admin/businesses";
    private const string PublicPath = "/api/businesses";

    /// <summary>Nepal Standard Time, stated here so the tests do not read it from the server.</summary>
    private static readonly TimeSpan NepalOffset = TimeSpan.FromMinutes(5 * 60 + 45);

    private readonly FixedClockApiFactory _factory = FixedClockApiFactory.NearNow();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_listing_is_open_inside_its_hours_and_closed_outside_them()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Clock Corner",
            published: true,
            isVerified: true,
            phone: "9800000013",
            openingHours: BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Wednesday, new TimeOnly(12, 0)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        // 20:00 Nepal is 14:15 UTC on the same date, so a server reading UTC would still call this
        // open. The two dates only differ for the small hours, so the offsets are also checked
        // there below.
        _factory.Clock.Set(NepalInstant(DayOfWeek.Wednesday, new TimeOnly(20, 0)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);

        // A check on the setup rather than on the product: 04:00 Nepal is a quarter past ten the
        // previous evening in UTC, so the two dates disagree here. If they ever agree the test has
        // stopped exercising the offset and the chosen hour needs moving.
        _factory.Clock.Set(NepalInstant(DayOfWeek.Wednesday, new TimeOnly(4, 0)));
        Assert.NotEqual(
            DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime),
            DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime.Add(NepalOffset)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Wednesday, new TimeOnly(9, 0)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);
    }

    [Fact]
    public async Task A_listing_with_no_hours_is_neither_open_nor_closed()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Unknown Hours Corner",
            published: true,
            isVerified: true,
            phone: "9800000014",
            openingHours: []));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Wednesday, new TimeOnly(12, 0)));

        // Unknown and closed are different statements, and a red closed badge for a business that
        // simply never listed its hours is telling the reader something untrue.
        Assert.Null((await PublishAndReadAsync(slug)).IsOpenNow);
    }

    [Fact]
    public async Task A_day_the_business_closed_is_closed_even_inside_its_usual_hours()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Sunday Closed Corner",
            published: true,
            isVerified: true,
            phone: "9800000015",
            openingHours:
            [
                BusinessApi.Day(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                BusinessApi.Day(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                BusinessApi.Day(DayOfWeek.Sunday, isClosed: true),
            ]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Saturday, new TimeOnly(12, 0)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Sunday, new TimeOnly(12, 0)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);
    }

    [Fact]
    public async Task A_shift_that_runs_past_midnight_is_still_open_in_the_small_hours()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        // Friday 22:00 to 02:00 belongs to Friday's row even though half of it is Saturday morning.
        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Late Night Corner",
            published: true,
            isVerified: true,
            phone: "9800000016",
            openingHours: [BusinessApi.Day(DayOfWeek.Friday, new TimeOnly(22, 0), new TimeOnly(2, 0))]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Friday, new TimeOnly(23, 30)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        // Saturday at one in the morning is still Friday's shift. Checking only Saturday's row here
        // would report closed for a bar that is obviously open.
        _factory.Clock.Set(NepalInstant(DayOfWeek.Saturday, new TimeOnly(1, 0)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Saturday, new TimeOnly(3, 0)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Saturday, new TimeOnly(22, 30)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);
    }

    [Fact]
    public async Task A_shift_that_runs_past_midnight_stops_at_its_closing_time()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Overnight Corner",
            published: true,
            isVerified: true,
            phone: "9800000017",
            openingHours: [BusinessApi.Day(DayOfWeek.Saturday, new TimeOnly(20, 0), new TimeOnly(4, 0))]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Saturday, new TimeOnly(21, 0)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Sunday, new TimeOnly(3, 59)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        // Four o'clock is the closing time itself, so the shift is over.
        _factory.Clock.Set(NepalInstant(DayOfWeek.Sunday, new TimeOnly(4, 0)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);
    }

    [Fact]
    public async Task A_shift_that_runs_past_midnight_does_not_leak_into_a_day_the_business_closed()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Closed Sunday Bar",
            published: true,
            isVerified: true,
            phone: "9800000018",
            openingHours:
            [
                BusinessApi.Day(DayOfWeek.Saturday, new TimeOnly(22, 0), new TimeOnly(2, 0)),
                BusinessApi.Day(DayOfWeek.Sunday, isClosed: true),
            ]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Saturday, new TimeOnly(23, 0)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        // A day the business closed on its own is a fact about that whole day, including the hours
        // after midnight that belong to it.
        _factory.Clock.Set(NepalInstant(DayOfWeek.Sunday, new TimeOnly(1, 0)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);
    }

    [Fact]
    public async Task A_day_the_business_never_listed_is_closed_rather_than_unknown()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            "Partial Week Corner",
            published: true,
            isVerified: true,
            phone: "9800000019",
            openingHours: [BusinessApi.Day(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0))]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The listing published hours, so it is closed rather than unknown on a day it left out.
        _factory.Clock.Set(NepalInstant(DayOfWeek.Wednesday, new TimeOnly(12, 0)));
        Assert.True((await PublishAndReadAsync(slug)).IsOpenNow);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Thursday, new TimeOnly(12, 0)));
        Assert.False((await PublishAndReadAsync(slug)).IsOpenNow);
    }

    [Fact]
    public async Task The_open_now_badge_is_not_derived_from_the_utc_clock()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        var slug = BusinessApi.NewSlug();

        // A unique token in the name, because the card is found by searching on it and the test
        // database is not empty: previous runs of this suite have left rows behind.
        var name = $"Offset Corner {Guid.NewGuid():N}";
        var created = await staffApi.PostForSnapshotAsync(AdminPath, BusinessApi.Body(
            slug,
            name,
            published: true,
            isVerified: true,
            phone: "9800000020",
            openingHours: BusinessApi.Week(new TimeOnly(9, 0), new TimeOnly(17, 0))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        _factory.Clock.Set(NepalInstant(DayOfWeek.Wednesday, new TimeOnly(12, 0)));

        var detail = await PublishAndReadAsync(slug);
        var card = await FirstCardAsync(name);

        // The same answer from the card and from the detail page: it is computed once from the same
        // rule rather than twice from two.
        Assert.True(detail.IsOpenNow);
        Assert.True(card);
    }

    /// <summary>
    /// The UTC instant at which it is the given Nepal weekday and wall-clock time. Stated as an
    /// offset rather than read back from an endpoint, so a server using the wrong offset fails these
    /// tests instead of agreeing with itself.
    /// </summary>
    private static DateTimeOffset NepalInstant(DayOfWeek day, TimeOnly time)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime.Add(NepalOffset));
        var ahead = ((int)day - (int)today.DayOfWeek + 7) % 7;

        return new DateTimeOffset(
            today.AddDays(ahead).ToDateTime(time, DateTimeKind.Unspecified),
            NepalOffset).ToUniversalTime();
    }

    private async Task<BusinessDetailResponse> PublishAndReadAsync(string slug)
    {
        using var reader = _factory.CreateClient();

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<BusinessDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success, $"The listing {slug} could not be read.");

        return envelope.Data!;
    }

    /// <summary>
    /// The card for one listing, read from the public list. Searched by the listing's name rather
    /// than its slug, because the slug is deliberately not a searchable field.
    /// </summary>
    private async Task<bool?> FirstCardAsync(string name)
    {
        using var reader = _factory.CreateClient();

        var page = await new AuthApiClient(reader).GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(name)}&pageSize=100");

        return Assert.Single(page.Items, item => item.Name == name).IsOpenNow;
    }
}

/// <summary>
/// The directory reuses the province and district reference data rather than copying it, so these
/// tests assert that the join those filters depend on is actually populated. A location filter that
/// silently returns nothing because a join is missing is a bug nobody would find without a test that
/// says so.
/// </summary>
public sealed class BusinessLocationFilterTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string PublicPath = "/api/businesses";

    [Theory]
    [InlineData("koshi", "Morang", "koshi-morang")]
    [InlineData("bagmati", "Lalitpur", "bagmati-lalitpur")]
    [InlineData("gandaki", "Kaski", "gandaki-kaski")]
    public async Task A_seeded_district_filter_returns_the_listings_that_sit_in_it(
        string provinceCode,
        string districtName,
        string districtCode)
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var districts = await api.GetAsync<List<DistrictResponse>>(
            $"/api/reference/provinces/{provinceCode}/districts");
        Assert.Contains(districts, district => district.Code == districtCode);

        var page = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?district={districtCode}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(districtName, item.DistrictName));
    }

    [Fact]
    public async Task A_seeded_province_filter_returns_only_that_province()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?province=karnali&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal("Karnali", item.ProvinceName));
    }

    [Fact]
    public async Task A_listing_reports_the_province_the_reference_data_says_its_district_is_in()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var provinces = await api.GetAsync<List<ProvinceResponse>>("/api/reference/provinces");
        var codeByName = provinces.ToDictionary(province => province.Name, province => province.Code);

        foreach (var province in provinces)
        {
            var districts = await api.GetAsync<List<DistrictResponse>>(
                $"/api/reference/provinces/{province.Code}/districts");

            foreach (var district in districts)
            {
                var page = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>(
                    $"{PublicPath}?district={district.Code}&pageSize=100");

                // Every listing in a district reports the province the reference endpoint says that
                // district belongs to, so the name on a card is looked up rather than asserted
                // against a value this test supplied to itself.
                Assert.All(page.Items, item =>
                {
                    Assert.Equal(district.Name, item.DistrictName);
                    Assert.True(
                        codeByName.TryGetValue(item.ProvinceName, out var code),
                        $"'{item.ProvinceName}' is not a province this site knows about.");
                    Assert.Equal(province.Code, code);
                });
            }
        }
    }

    [Fact]
    public async Task A_district_filter_wins_over_the_province_it_sits_in()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // The district is the narrower filter, so naming both is not a contradiction and the answer
        // is the district rather than an empty set.
        var page = await api.GetAsync<PagedResponse<BusinessSummaryResponse>>(
            $"{PublicPath}?province=bagmati&district=koshi-bhojpur&pageSize=100");

        Assert.Empty(page.Items);
    }
}

internal static class BusinessApi
{
    /// <summary>
    /// Comfortably longer than the sixty character minimum that publishing requires, so the publish
    /// rule is satisfied by the fixture itself rather than by each test padding itself out.
    /// </summary>
    public const string LongDescription =
        "This description is comfortably longer than the sixty character minimum that publishing "
        + "requires, so the publish rule is satisfied by the fixture itself rather than by each test "
        + "padding itself out with filler sentences about nothing in particular.";

    public static string NewSlug() => $"test-{Guid.NewGuid():N}";

    public static object Day(DayOfWeek day, TimeOnly? opensAt = null, TimeOnly? closesAt = null, bool isClosed = false) =>
        new
        {
            dayOfWeek = day,
            isClosed,
            opensAt = opensAt?.ToString("HH:mm:ss"),
            closesAt = closesAt?.ToString("HH:mm:ss"),
        };

    /// <summary>Seven identical open days, Sunday first.</summary>
    public static IReadOnlyList<object> Week(TimeOnly opensAt, TimeOnly closesAt) =>
        [.. Enum.GetValues<DayOfWeek>().Select(day => Day(day, opensAt, closesAt))];

    /// <summary>
    /// A listing fixture. The name carries a unique token so the row a test has just written can be
    /// found again with a text search, which is the handle the search endpoint offers: it matches the
    /// name, the description and the address. A shared name would make every test row match every
    /// other one.
    /// </summary>
    public static object Body(
        string slug,
        string name,
        bool published,
        string? name2 = null,
        string? description = null,
        string category = "Other",
        string? districtCode = "bagmati-lalitpur",
        string? address = null,
        string? phone = null,
        string? email = null,
        string? website = null,
        bool isVerified = false,
        IReadOnlyList<object>? openingHours = null) => new
        {
            slug,
            name = name2 ?? name,
            description = description ?? LongDescription,
            category,
            districtCode,
            address,
            phone,
            email,
            website,
            isPublished = published,
            isVerified,
            openingHours = openingHours ?? [],
        };

    /// <summary>
    /// Unwraps the listing a staff write returned, so a test can assert against the record the
    /// server stored rather than against the request that was sent.
    /// </summary>
    public static BusinessAdminResponse ReadCreated(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<BusinessAdminResponse>>(TestJson.Options);

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

/// <summary>
/// Phone numbers are stored as digits only, so one business cannot appear under two spellings of
/// the same number. The rule is loose about prefix and area codes, which are not settled, because a
/// wrong rejection stops a legitimate business from being listed — but it is not so loose that
/// letters are quietly discarded.
/// </summary>
public sealed class BusinessPhoneTests
{
    [Theory]
    [InlineData("9841234567", "9841234567")]
    [InlineData("+977 9841234567", "9841234567")]
    [InlineData("977-9841234567", "9841234567")]
    [InlineData("01-4567890", "014567890")]
    [InlineData("4567890", "4567890")]
    [InlineData("(01) 4567890", "014567890")]
    public void A_plausible_nepali_number_is_reduced_to_its_digits(string raw, string expected)
    {
        Assert.True(BusinessService.TryNormalisePhone(raw, out var normalised, out var error));
        Assert.Equal(expected, normalised);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12345678901234")]
    public void A_number_with_the_wrong_number_of_digits_is_refused(string raw)
    {
        Assert.False(BusinessService.TryNormalisePhone(raw, out var normalised, out var error));
        Assert.Null(normalised);
        Assert.NotNull(error);
    }

    /// <summary>
    /// Dropping letters would turn "abc1234567" into a valid number, so a mistyped field would be
    /// stored looking checked rather than rejected while it is still being corrected.
    /// </summary>
    [Theory]
    [InlineData("abc1234567")]
    [InlineData("9841234567ext")]
    [InlineData("call 9841234567")]
    [InlineData("9841234567#")]
    public void A_number_carrying_anything_but_digits_and_punctuation_is_refused(string raw)
    {
        Assert.False(BusinessService.TryNormalisePhone(raw, out var normalised, out var error));
        Assert.Null(normalised);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_number_is_left_blank_rather_than_refused(string? raw)
    {
        Assert.True(BusinessService.TryNormalisePhone(raw, out var normalised, out var error));
        Assert.Null(normalised);
        Assert.Null(error);
    }
}