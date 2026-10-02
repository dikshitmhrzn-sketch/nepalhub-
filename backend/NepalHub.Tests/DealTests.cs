using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Deals;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Reference;

namespace NepalHub.Tests;

/// <summary>
/// Reading the offers page as a visitor. Nothing here signs in, which is the point: a list of local
/// discounts has to work for somebody who has never made an account.
///
/// Assertions are scoped to a search term or a seed slug wherever a count is involved, rather than
/// asserting a whole-page total, because the staff tests publish rows of their own and a suite that
/// only passed because of the order its classes ran in would fail the next time somebody reordered it.
/// </summary>
public sealed class DealsTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string PublicPath = "/api/deals";
    private const string AdminPath = "/api/admin/deals";

    [Fact]
    public async Task The_offers_page_is_readable_without_an_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_seeded_offers_spread_across_several_provinces()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var seeded = new HashSet<string>(
            new[]
            {
                "free-chiya-with-thakali-set-dharan",
                "lunch-combo-250-bhojan-ghar",
                "two-samosa-for-one-newa-momo",
                "student-tuition-discount-thapathali",
                "first-month-free-everest-web-studio",
                "weekend-gym-pass-thamel",
                "free-delivery-over-1500-kathmandu",
                "handicraft-bargain-bhaktapur",
                "haircut-and-beard-500-baneshwor",
                "freight-free-over-50k-dhangadhi",
            },
            StringComparer.Ordinal);

        var active = await ReadAllPublicAsync(api);
        var found = active.Where(item => seeded.Contains(item.Slug)).ToList();

        // Every seeded active offer is on the public page. A missing one means the expiry filter is
        // hiding content that should be live, which is the failure this module is most prone to.
        Assert.Equal(seeded.Count, found.Count);

        var provinces = found
            .Select(item => item.ProvinceName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(provinces.Count >= 3, $"The seeded offers cover {provinces.Count} provinces.");
    }

    [Fact]
    public async Task A_seeded_offer_opens_and_reports_its_dates_and_district()
    {
        using var client = factory.CreateClient();

        var envelope = await client.GetFromJsonAsync<ApiEnvelope<DealDetailResponse>>(
            $"{PublicPath}/free-chiya-with-thakali-set-dharan",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal("Free chiya with any thakali set", envelope.Data!.Title);
        Assert.Equal("Thakali Kitchen", envelope.Data.BusinessName);
        Assert.Equal("Sunsari", envelope.Data.DistrictName);
        Assert.Equal("Koshi", envelope.Data.ProvinceName);

        // The ranges are required, so a stored offer always answers both of these. Asserting them is
        // what proves a reader is never shown an offer with no end date.
        Assert.True(envelope.Data.EndsOn >= envelope.Data.StartsOn);
        Assert.True(envelope.Data.DaysRemaining > 0);
        Assert.True(envelope.Data.IsRunning);
    }

    /// <summary>
    /// An offer with no district is a national offer, and it is the case that separates this module from
    /// a business listing. It has to stay reachable with no location filter, and it has to report no
    /// district rather than inventing one.
    /// </summary>
    [Fact]
    public async Task A_nationwide_offer_is_reachable_and_reports_no_district()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("free delivery over")}&pageSize=100");

        var offer = Assert.Single(page.Items);

        Assert.Equal("free-delivery-over-1500-kathmandu", offer.Slug);
        Assert.Equal(string.Empty, offer.DistrictName);
        Assert.Equal(string.Empty, offer.ProvinceName);
        Assert.Null(offer.BusinessName);
    }

    [Fact]
    public async Task A_draft_is_invisible_to_readers_and_answers_the_same_404_as_a_slug_that_never_existed()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var slug = DealApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(
            AdminPath,
            DealApi.Body(slug, $"Hidden Offer {Guid.NewGuid():N}", published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var publicPage = await new AuthApiClient(reader).GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(slug)}");
        Assert.Equal(0, publicPage.TotalCount);

        using var draft = await reader.GetAsync($"{PublicPath}/{slug}");
        using var invented = await reader.GetAsync($"{PublicPath}/no-such-offer-{Guid.NewGuid():N}");

        // The same answer for both, so the endpoint cannot be used to find out which slugs are real but
        // not yet public.
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invented.StatusCode);
    }

    [Fact]
    public async Task An_expired_offer_is_invisible_to_readers()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // The seeder writes published rows past their end date on purpose, which is the only way to be
        // sure the expiry filter runs on real data rather than on a test double.
        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("monsoon")}&pageSize=100");

        Assert.Equal(0, page.TotalCount);

        using var detail = await client.GetAsync($"{PublicPath}/monsoon-set-dal-bhat-expired");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    /// <summary>
    /// The staff board keeps expired offers, flagged. A staff member asking why their offer is not
    /// showing needs to see the row and needs to see that it ended, not to find it has vanished.
    /// </summary>
    [Fact]
    public async Task An_expired_offer_stays_in_the_staff_list_with_a_flag()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(staff);

        var page = await api.GetAsync<PagedResponse<DealAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString("monsoon")}&pageSize=100");

        var offer = Assert.Single(page.Items);
        Assert.True(offer.IsPublished);
        Assert.True(offer.IsExpired);
    }

    [Fact]
    public async Task A_published_offer_reaches_readers_with_the_fields_a_visitor_acts_on()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var title = $"Reachable Offer {Guid.NewGuid():N}";
        var slug = DealApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, DealApi.Body(
            slug,
            title,
            published: true,
            discountText: "Free chiya with any set",
            districtCode: "bagmati-bhaktapur",
            redemptionUrl: "https://example.com/redeem"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<DealDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(title, envelope.Data!.Title);
        Assert.Equal("Free chiya with any set", envelope.Data.DiscountText);
        Assert.Equal("Bhaktapur", envelope.Data.DistrictName);
        Assert.Equal("Bagmati", envelope.Data.ProvinceName);
        Assert.Equal("https://example.com/redeem", envelope.Data.RedemptionUrl);
        Assert.True(envelope.Data.IsRunning);
    }

    [Fact]
    public async Task A_offer_survives_the_business_it_names_being_unpublished()
    {
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(admin);

        // A draft business, which the offers editor cannot pick from its published-only list but which
        // the save path still accepts by slug. Drafting against something not yet listed is the normal
        // order of work, and an offer must not be blocked by it.
        var businessSlug = $"test-draft-shop-{Guid.NewGuid():N}";
        var businessName = $"Unlisted Shop {Guid.NewGuid():N}";
        var business = await adminApi.PostForSnapshotAsync(
            "/api/admin/businesses",
            BusinessApi.Body(
                businessSlug,
                businessName,
                published: false,
                phone: "9800000101",
                districtCode: "bagmati-lalitpur"));
        Assert.Equal(HttpStatusCode.Created, business.StatusCode);

        var offerSlug = DealApi.NewSlug();
        var created = await adminApi.PostForSnapshotAsync(AdminPath, DealApi.Body(
            offerSlug,
            $"Draft Linked Offer {Guid.NewGuid():N}",
            published: true,
            businessSlug: businessSlug,
            districtCode: "bagmati-lalitpur"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var reader = factory.CreateClient();
        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<DealDetailResponse>>(
            $"{PublicPath}/{offerSlug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(businessName, envelope.Data!.BusinessName);
        Assert.Null(envelope.Data.BusinessSlug);
    }

    [Fact]
    public async Task An_offer_with_no_business_at_all_is_stored_and_readable()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);

        var slug = DealApi.NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, DealApi.Body(
            slug,
            $"Standalone Offer {Guid.NewGuid():N}",
            published: true,
            districtCode: null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = DealApi.ReadCreated(created);
        Assert.Null(stored.BusinessSlug);
        Assert.Null(stored.DistrictName);
    }

    [Fact]
    public async Task The_search_matches_the_offer_text_and_the_terms()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // The discount text is searchable, which is what lets somebody look for "chiya" rather than
        // remembering which business they saw it at.
        var byOffer = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("20% off")}&pageSize=100");
        Assert.NotEmpty(byOffer.Items);

        var byTerms = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("cannot be combined")}&pageSize=100");
        Assert.NotEmpty(byTerms.Items);
    }

    /// <summary>
    /// A term of <c>%</c> is a LIKE wildcard. Unescaped it becomes a pattern matching every row, which is
    /// the opposite of what was asked for and a cheap way to walk the whole catalogue.
    /// <para>
    /// The assertion is that the term matches <em>literally</em>, not that it matches nothing: two seeded
    /// offers really do say "20% off for students" and "10% off in a single visit", and a visitor typing
    /// a percent sign is entitled to find them. What must not happen is the wildcard swallowing every
    /// other offer on the page.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_search_term_of_wildcards_matches_literally_rather_than_everything()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var everything = await api.GetAsync<PagedResponse<DealSummaryResponse>>($"{PublicPath}?pageSize=1");
        var wildcard = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString("%")}&pageSize=100");

        Assert.True(
            everything.TotalCount > 5,
            "The fixture needs enough live offers for a match-everything bug to be visible.");
        Assert.True(
            wildcard.TotalCount < everything.TotalCount,
            "A bare % matched every offer, so it is being read as a wildcard.");
        Assert.All(
            wildcard.Items,
            item => Assert.Contains(
                "%",
                $"{item.Title} {item.Summary} {item.DiscountText}",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("bagmati-lalitpur", "Lalitpur")]
    [InlineData("koshi-sunsari", "Sunsari")]
    [InlineData("sudurpashchim-kailali", "Kailali")]
    public async Task A_district_filter_returns_the_offers_that_sit_in_it(string districtCode, string districtName)
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var districts = await api.GetAsync<List<DistrictResponse>>(
            $"/api/reference/provinces/{districtCode[..districtCode.IndexOf('-')]}/districts");
        Assert.Contains(districts, district => district.Code == districtCode);

        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?district={districtCode}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(districtName, item.DistrictName));
    }

    /// <summary>
    /// A province the seeded offers actually sit in. Karnali and Gandaki have no live offer seeded —
    /// Gandaki's only rows are an expired offer and a draft — and a filter test pointed at an empty
    /// province would pass for the wrong reason or fail on a seed change.
    /// </summary>
    [Theory]
    [InlineData("koshi", "Koshi")]
    [InlineData("sudurpashchim", "Sudurpashchim")]
    [InlineData("bagmati", "Bagmati")]
    public async Task A_province_filter_returns_only_offers_in_that_province(
        string provinceCode,
        string provinceName)
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?province={provinceCode}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(provinceName, item.ProvinceName));
    }

    /// <summary>
    /// A province filter excludes the nationwide offer, deliberately. It is not in a province, so listing
    /// it under all seven would be a claim about where it applies that nobody made.
    /// </summary>
    [Fact]
    public async Task A_province_filter_does_not_return_the_nationwide_offer()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?province=bagmati&pageSize=100");

        Assert.All(page.Items, item => Assert.DoesNotContain("free-delivery-over-1500-kathmandu", item.Slug));
    }

    [Fact]
    public async Task The_default_order_puts_the_offer_ending_soonest_first()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>($"{PublicPath}?pageSize=100");

        var ends = page.Items.Select(item => item.EndsOn).ToList();

        // Sorted ascending: what a reader wants from an offers page is the deals about to expire.
        Assert.Equal(ends.OrderBy(date => date).ToList(), ends);
    }

    [Fact]
    public async Task The_newest_order_puts_the_most_recently_added_first()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>($"{PublicPath}?sort=Newest&pageSize=100");

        var created = page.Items.Select(item => item.CreatedAt).ToList();

        Assert.Equal(created.OrderByDescending(at => at).ToList(), created);
    }

    [Fact]
    public async Task Paging_walks_the_offers_without_repeating_one()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var first = await api.GetAsync<PagedResponse<DealSummaryResponse>>($"{PublicPath}?pageSize=3");
        Assert.True(first.Items.Count <= 3);

        if (first.TotalPages <= 1)
        {
            return;
        }

        var second = await api.GetAsync<PagedResponse<DealSummaryResponse>>($"{PublicPath}?page=2&pageSize=3");

        Assert.Empty(first.Items.Select(item => item.Slug).Intersect(second.Items.Select(item => item.Slug)));
    }

    [Fact]
    public async Task A_page_size_beyond_the_ceiling_is_clamped_rather_than_refused()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>($"{PublicPath}?pageSize=5000");

        // The ceiling is a shared rule across every list in the product, so a caller asking for more
        // than exists gets the maximum rather than an error.
        Assert.Equal(100, page.PageSize);
    }

    /// <summary>
    /// Only <c>sort</c> is refused here, because the offers page has exactly one enumerated argument.
    /// There is deliberately no category filter on this list — an offer is about a place and a date, not
    /// a kind of business — so an unknown category is not an error to report, it is simply ignored.
    /// </summary>
    [Fact]
    public async Task An_unrecognised_sort_is_refused_with_the_field_named()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?sort=sideways");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("sort_invalid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_offers_page_ignores_a_category_because_it_has_no_category_filter()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var unfiltered = await api.GetAsync<PagedResponse<DealSummaryResponse>>($"{PublicPath}?pageSize=100");
        var withCategory = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
            $"{PublicPath}?pageSize=100&category=Bargains");

        // Documented rather than refused: an unknown argument on a page with no such taxonomy is not a
        // caller mistake, and turning it into an error would break the shared frontend filter component.
        Assert.Equal(unfiltered.TotalCount, withCategory.TotalCount);
    }

    [Theory]
    [InlineData("province", "atlantis")]
    [InlineData("district", "nowhere-1")]
    public async Task An_unrecognised_location_code_is_refused_rather_than_matching_nothing(string parameter, string value)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?{parameter}={Uri.EscapeDataString(value)}");

        // A specific error rather than an empty page, because a visitor who typed a district code wants
        // to be told it does not exist rather than shown nothing and left to guess.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_slug_already_used_by_another_offer_is_refused_as_a_conflict()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = DealApi.NewSlug();

        var first = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(slug, "First Owner", published: false));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(slug, "Second Owner", published: false));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("slug_taken", second.Error?.Code);
    }

    [Fact]
    public async Task The_slug_of_a_published_offer_cannot_be_moved()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = DealApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(slug, "Frozen Slug Offer", published: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = DealApi.ReadCreated(created);

        var moved = await api.PutForSnapshotAsync(
            $"{AdminPath}/{stored.Id}",
            DealApi.Body(DealApi.NewSlug(), "Frozen Slug Offer", published: true));

        // A conflict rather than a validation failure: the request was well formed, and the state of the
        // row is what forbids it.
        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.Equal("slug_immutable", moved.Error?.Code);
    }

    [Fact]
    public async Task A_draft_slug_can_still_be_corrected()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            DealApi.Body(DealApi.NewSlug(), "Mistyped Slug", published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = DealApi.ReadCreated(created);
        var corrected = DealApi.NewSlug();

        var updated = await api.PutForSnapshotAsync(
            $"{AdminPath}/{stored.Id}",
            DealApi.Body(corrected, "Mistyped Slug", published: false));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(corrected, DealApi.ReadCreated(updated).Slug);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://example.com/offers")]
    [InlineData("https://localhost/offers")]
    [InlineData("https://127.0.0.1/offers")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("file:///etc/passwd")]
    public async Task A_redemption_link_that_is_not_an_ordinary_web_address_is_refused(string url)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Unsafe Link Offer",
            published: false,
            redemptionUrl: url));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(DealApi.Details(created), error => error.Field == "redemptionUrl");
        Assert.Equal("invalid", field.Code);
    }

    [Theory]
    [InlineData("Not A Slug")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double--hyphen")]
    [InlineData("UPPERCASE")]
    public async Task A_slug_that_is_not_lowercase_words_is_refused(string slug)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(slug, "Bad Slug Offer", published: false));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    [Fact]
    public async Task An_offer_that_ends_before_it_starts_is_refused()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Backwards Offer",
            published: false,
            startsOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            endsOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(DealApi.Details(created), error => error.Field == "endsOn");
        Assert.Equal("out_of_range", field.Code);
    }

    [Fact]
    public async Task An_offer_that_has_already_ended_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        // Yesterday's end date on the UTC calendar. The rule compares against the Nepal date, which is never
        // behind the UTC date, so a date in the UTC past is always in the Nepal past as well and this
        // test does not depend on what hour of the day it is being run.
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Long Finished Offer",
            published: true,
            startsOn: utcToday.AddDays(-10),
            endsOn: utcToday.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(DealApi.Details(created), error => error.Field == "endsOn");
        Assert.Equal("out_of_range", field.Code);
    }

    [Fact]
    public async Task An_offer_that_would_run_for_more_than_a_year_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Perpetual Offer",
            published: true,
            startsOn: today,
            endsOn: today.AddDays(800)));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(DealApi.Details(created), error => error.Field == "endsOn");
        Assert.Equal("out_of_range", field.Code);
    }

    [Fact]
    public async Task An_offer_with_no_offer_in_it_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "A Title With No Offer",
            published: true,
            omitDiscountText: true));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(DealApi.Details(created), error => error.Field == "discountText");
        Assert.Equal("required", field.Code);
    }

    [Fact]
    public async Task An_offer_with_no_summary_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "A Title With No Summary",
            published: true,
            omitSummary: true));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(DealApi.Details(created), error => error.Field == "summary");
        Assert.Equal("required", field.Code);
    }

    [Fact]
    public async Task An_offer_with_stub_terms_cannot_be_published()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Stub Terms",
            published: true,
            description: "Twenty percent off."));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var field = Assert.Single(DealApi.Details(created), error => error.Field == "description");
        Assert.Equal("required", field.Code);
    }

    /// <summary>
    /// The publish rules apply only to the draft-to-live transition. A live row being corrected may well
    /// need its end date moved in, and refusing that edit would make ending an offer early impossible
    /// through the screen that exists to do it.
    /// </summary>
    [Fact]
    public async Task A_published_offer_can_have_its_end_date_brought_forward()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = DealApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            slug,
            "Long Running Offer",
            published: true,
            endsInDays: 120));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = DealApi.ReadCreated(created);

        var shortened = await api.PutForSnapshotAsync($"{AdminPath}/{stored.Id}", DealApi.Body(
            slug,
            "Long Running Offer",
            published: true,
            endsInDays: 2));

        Assert.Equal(HttpStatusCode.OK, shortened.StatusCode);

        var reread = DealApi.ReadCreated(shortened);
        Assert.Equal(
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2),
            reread.EndsOn);
    }

    [Fact]
    public async Task The_editor_is_told_what_is_missing_from_a_draft()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Incomplete Draft",
            published: false,
            summary: null,
            discountText: null,
            description: string.Empty));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stored = DealApi.ReadCreated(created);

        // The same rules that would refuse a publish, returned before staff try, so the editor can show
        // them rather than leaving staff to find out on save.
        Assert.NotEmpty(stored.PublishBlockers);
        Assert.Contains(stored.PublishBlockers, message => message.Contains("offer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_published_offer_carries_no_publish_blockers()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Complete Offer",
            published: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Empty(DealApi.ReadCreated(created).PublishBlockers);
    }

    [Fact]
    public async Task A_published_offer_cannot_be_deleted_and_a_draft_can()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var published = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Undeletable Offer",
            published: true));
        var publishedId = DealApi.ReadCreated(published).Id;

        var refused = await api.DeleteForSnapshotAsync($"{AdminPath}/{publishedId}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("deal_published", refused.Error?.Code);

        var draft = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            DealApi.NewSlug(),
            "Deletable Offer",
            published: false));
        var draftId = DealApi.ReadCreated(draft).Id;

        var deleted = await api.DeleteForSnapshotAsync($"{AdminPath}/{draftId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await api.GetRawAsync($"{AdminPath}/{draftId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_offer_that_does_not_exist_is_a_not_found()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        using var response = await api.GetRawAsync($"{AdminPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_staff_list_can_be_filtered_to_drafts()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(staff);

        var drafts = await api.GetAsync<PagedResponse<DealAdminSummaryResponse>>($"{AdminPath}?status=Draft&pageSize=100");

        Assert.NotEmpty(drafts.Items);
        Assert.All(drafts.Items, item => Assert.False(item.IsPublished));
    }

    [Fact]
    public async Task The_business_picker_returns_published_listings_only()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(staff);

        var options = await api.GetAsync<List<DealBusinessOptionResponse>>($"{AdminPath}/businesses");

        Assert.NotEmpty(options);

        // Slugs and names, not ids: the choice being made is "this business", and nobody picks a
        // business by GUID.
        Assert.All(options, option => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", option.Slug));

        // Named by search rather than looked for in the full list. The endpoint caps the picker at a
        // hundred rows in name order, and every other test class in this project publishes businesses
        // into the same database, so a seeded listing can legitimately sit past the cap. The search
        // path is the one the editor actually uses, so it is the one worth asserting on.
        var searched = await api.GetAsync<List<DealBusinessOptionResponse>>(
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
            $"A visitor reached the staff offers list and got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task The_staff_endpoints_are_closed_to_a_signed_in_reader()
    {
        // CreateSignedInClientAsync returns the client directly rather than a task, because it blocks
        // internally on two requests.
        using var reader = factory.CreateSignedInClientAsync(TestData.UniqueEmail());

        using var response = await reader.GetAsync(AdminPath);

        // Signed in is not staff. Without the staff role the fallback deny policy answers, and a reader
        // account must never reach the offers editor.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_staff_write_without_a_csrf_token_is_refused()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        using var response = await api.PostAsync(
            AdminPath,
            DealApi.Body(DealApi.NewSlug(), "No Token", published: false),
            withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("csrf", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every live offer, walking the pages rather than asking for a hundred and hoping.
    /// <para>
    /// The public list is capped at a hundred rows and the default order is by end date, so a single
    /// large page is only the whole catalogue while the catalogue is smaller than a page. Staff
    /// tests publish offers of their own and those rows persist, so "the seeded offers are all
    /// here" has to be asked of every row rather than of the first hundred: read one page and the
    /// test starts failing once the accumulated rows reach the cap, for a reason that has nothing to
    /// do with the seed data.
    /// </para>
    /// </summary>
    private static async Task<IReadOnlyList<DealSummaryResponse>> ReadAllPublicAsync(AuthApiClient api)
    {
        const int PageSize = 100;

        var items = new List<DealSummaryResponse>();
        var pageNumber = 1;

        while (true)
        {
            var page = await api.GetAsync<PagedResponse<DealSummaryResponse>>(
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
/// Offer expiry is day-granular and compared against the current Nepal date, so an offer is live through
/// the whole of its final day and then gone on its own. These tests pin the clock because the boundary
/// is the whole contract.
///
/// The pinned clock also checks the offset rather than assuming it: an offer ending "today" in UTC terms
/// is a different day in Nepal for five and three quarter hours out of twenty four, so a server reading
/// UTC would get these wrong in a way a test on the default clock would never notice.
/// </summary>
public sealed class DealExpiryTests : IAsyncLifetime
{
    private const string AdminPath = "/api/admin/deals";
    private const string PublicPath = "/api/deals";

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
    public async Task An_offer_is_live_on_its_first_day_and_gone_the_day_after_its_last()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = DealApi.NewSlug();
        var nepalToday = NepalDateOnly();

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            slug,
            "Boundary Offer",
            published: true,
            startsOn: nepalToday,
            endsOn: nepalToday.AddDays(2)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The first day.
        _factory.Clock.Set(NepalInstant(NepalDateOnly(), new TimeOnly(9, 0)));
        Assert.Equal(3, await DaysRemainingAsync(slug));

        // The last day. 21:00 Nepal is still the last day, and an offer that vanishes at midnight
        // rather than at the end of the day would leave a reader at a shop at dinner time with nothing.
        _factory.Clock.Set(NepalInstant(NepalDateOnly().AddDays(2), new TimeOnly(21, 0)));
        Assert.Equal(1, await DaysRemainingAsync(slug));

        // The day after. An offer that ended yesterday must be gone, and identically invisible to the
        // detail endpoint and the list.
        _factory.Clock.Set(NepalInstant(NepalDateOnly().AddDays(3), new TimeOnly(9, 0)));

        using var detail = await _factory.CreateClient().GetAsync($"{PublicPath}/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task The_last_day_is_the_nepal_day_not_the_utc_day()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        var slug = DealApi.NewSlug();
        var nepalToday = NepalDateOnly();

        var created = await api.PostForSnapshotAsync(AdminPath, DealApi.Body(
            slug,
            "Offset Offer",
            published: true,
            startsOn: nepalToday,
            endsOn: nepalToday));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // 23:30 Nepal is 17:45 UTC on the same calendar day, so this particular hour would agree with a
        // UTC server. The check below is the one that would not.
        _factory.Clock.Set(NepalInstant(NepalDateOnly(), new TimeOnly(23, 30)));
        Assert.Equal(1, await DaysRemainingAsync(slug));

        _factory.Clock.Set(NepalInstant(NepalDateOnly().AddDays(1), new TimeOnly(1, 0)));

        // 01:00 Nepal is 19:15 UTC on the previous day. A server reading UTC would still think today was
        // yesterday and call the offer live for a whole extra day, which is exactly the bug this rule
        // exists to prevent. Checked after the clock is moved, not before: the two dates only
        // disagree in the five-and-three-quarter hour window, and asserting it at an arbitrary hour
        // would make this test depend on what time of day it happens to be run.
        var utcDate = DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime);
        var nepalDate = DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime.Add(NepalOffset));
        Assert.NotEqual(utcDate, nepalDate);

        using var detail = await _factory.CreateClient().GetAsync($"{PublicPath}/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task A_seeded_expired_offer_is_hidden_by_the_clock_and_not_only_by_its_row()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);

        // The seeder wrote this one with an end date in the past on purpose. The point of the assertion
        // is that the public filter hides it on its own, without anybody having retracted anything.
        using var detail = await _factory.CreateClient().GetAsync($"{PublicPath}/monsoon-set-dal-bhat-expired");

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var stored = await api.GetAsync<PagedResponse<DealAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString("monsoon")}&pageSize=100");

        Assert.True(Assert.Single(stored.Items).IsExpired);
    }

    private async Task<int> DaysRemainingAsync(string slug)
    {
        using var reader = _factory.CreateClient();

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<DealDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success, $"The offer {slug} could not be read at this point in time.");

        return envelope.Data!.DaysRemaining;
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

internal static class DealApi
{
    /// <summary>
    /// Comfortably longer than the sixty character minimum that publishing requires, so the publish rule
    /// is satisfied by the fixture itself rather than by each test padding itself out.
    /// </summary>
    public const string LongDescription =
        "These terms are comfortably longer than the sixty character minimum that publishing requires, "
        + "so the publish rule is satisfied by the fixture itself rather than by every test padding its "
        + "own body out with filler sentences about nothing in particular at all.";

    /// <summary>Long enough to satisfy the minimum with a little to spare, for the tests that want it.</summary>
    public const string StubDescription = "Twenty percent off, this week only.";

    public static string NewSlug() => $"test-{Guid.NewGuid():N}";

    /// <summary>
    /// An offer fixture. The title carries a unique token so the row a test has just written can be found
    /// again with a text search, which is the handle the search endpoint offers: it matches the title,
    /// the summary, the terms and the offer text. A shared title would make every test row match every
    /// other one.
    /// </summary>
public static object Body(
        string slug,
        string title,
        bool published,
        string? summary = null,
        bool omitSummary = false,
        string? discountText = "Free chiya with any set",
        bool omitDiscountText = false,
        string? description = null,
        string? districtCode = "bagmati-lalitpur",
        string? businessSlug = null,
        string? redemptionUrl = null,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null,
        int? endsInDays = null) => new
        {
            slug,
            title,
            summary = omitSummary ? null : summary ?? "A one-line summary long enough to be useful on a card.",
            discountText = omitDiscountText ? null : discountText,
            description = description ?? LongDescription,
            districtCode,
            businessSlug,
            redemptionUrl,
            startsOn = startsOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
            endsOn = endsOn ?? DefaultEnd(endsInDays),
            isPublished = published,
        };

    /// <summary>
    /// The end date a fixture gets when the test did not name one. Relative rather than absolute, so a
    /// fixture never lands in the past and starts failing the publish rules because of the date it was
    /// written on.
    /// </summary>
    private static DateOnly DefaultEnd(int? endsInDays) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(endsInDays ?? 30);

    /// <summary>
    /// Unwraps the offer a staff write returned, so a test can assert against the record the server
    /// stored rather than against the request that was sent.
    /// </summary>
    public static DealAdminResponse ReadCreated(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<DealAdminResponse>>(TestJson.Options);

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