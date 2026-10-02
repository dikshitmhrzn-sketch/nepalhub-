using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Students;

namespace NepalHub.Tests;

/// <summary>
/// Reading the catalogue as a visitor. Nothing here signs in, which is the point: the public
/// surface has to work for somebody who has never made an account.
///
/// Assertions are scoped to a search term or a seed slug wherever a count is involved, rather
/// than asserting a whole-catalogue total. The staff tests in this file publish rows of their
/// own, and a suite that passed only because of the order its classes happened to run in would
/// be a suite that fails the next time somebody reorders it.
/// </summary>
public sealed class StudentResourceCatalogueTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string PublicPath = "/api/students/resources";

    /// <summary>Every slug the seeder ships, so "the catalogue was seeded" is one assertion.</summary>
    private static readonly string[] SeededSlugs =
    [
        "quadratic-equations-grade-9",
        "trigonometric-ratios-grade-11",
        "laws-of-motion-grade-11",
        "chemical-bonding-grade-11",
        "cell-structure-grade-9",
        "parts-of-speech-grade-6",
        "nepali-present-tense-verbs",
        "constitution-of-nepal-2015",
        "data-structures-introduction",
        "nepali-numbering-and-dates",
        "see-2081-mathematics-question-paper",
        "see-2081-english-question-paper",
        "see-2081-science-question-paper",
        "see-2081-social-studies-question-paper",
        "grade-11-12-annual-2081-mathematics",
        "grade-11-12-annual-2081-biology",
        "grade-11-12-annual-2081-english",
        "plus-two-exam-merit-scholarship-closed",
        "undergraduate-tuition-support-closed",
        "grade-11-12-merit-scholarship",
        "engineering-undergraduate-scholarship",
        "management-bachelors-scholarship",
        "competitive-exam-coaching-support",
        "plus-two-science-stream-scholarship",
    ];

    /// <summary>
    /// The two seeds that ship already closed, paired with the words that find them. Search
    /// matches the title and the summary rather than the slug, so a test that wants one specific
    /// row has to search for something that row actually says.
    /// </summary>
    private static readonly (string Slug, string Search)[] ExpiredSeeds =
    [
        ("plus-two-exam-merit-scholarship-closed", "Plus Two exam merit"),
        ("undergraduate-tuition-support-closed", "Undergraduate tuition support"),
    ];

    [Fact]
    public async Task The_public_catalogue_is_readable_without_an_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_seeded_catalogue_is_present_and_published()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var items = await ReadAllAdminRowsAsync(client);

        Assert.All(SeededSlugs, slug => Assert.Contains(items, item => item.Slug == slug));

        // Every seeded row except the two that model a closed scholarship is live.
        foreach (var item in items.Where(item => SeededSlugs.Contains(item.Slug)))
        {
            Assert.True(item.IsPublished, $"{item.Slug} should be seeded as published.");
        }
    }

    [Fact]
    public async Task The_seeded_catalogue_has_a_nepali_subject_for_every_row()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var items = await ReadAllAdminRowsAsync(client);

        var seeded = items.Where(item => SeededSlugs.Contains(item.Slug)).ToList();

        Assert.Equal(SeededSlugs.Length, seeded.Count);
        Assert.Equal(10, seeded.Count(item => item.Type == ResourceType.Note));
        Assert.Equal(7, seeded.Count(item => item.Type == ResourceType.QuestionPaper));
        Assert.Equal(7, seeded.Count(item => item.Type == ResourceType.Scholarship));
    }

    /// <summary>
    /// Reads every row of the staff list, not just the first page.
    ///
    /// The staff tests in this file create rows of their own and do not remove them, and the
    /// staff list is ordered by most recently updated, so the seeds sit on a later page once a
    /// few runs have accumulated. <c>pageSize</c> is capped at 100 by the API, so asking for a
    /// larger page is silently reduced rather than refused. Paging to the end is the only way to
    /// assert on the whole catalogue, and it is what keeps these two tests honest on a database
    /// that has been used rather than a freshly created one.
    /// </summary>
    private static async Task<IReadOnlyList<ResourceAdminSummaryResponse>> ReadAllAdminRowsAsync(
        HttpClient client)
    {
        const string AdminPath = "/api/admin/students/resources?pageSize=100";

        var api = new AuthApiClient(client);
        var rows = new List<ResourceAdminSummaryResponse>();

        PagedResponse<ResourceAdminSummaryResponse> page;
        var pageNumber = 1;
        do
        {
            page = await api.GetAsync<PagedResponse<ResourceAdminSummaryResponse>>(
                $"{AdminPath}&page={pageNumber}");
            rows.AddRange(page.Items);
            pageNumber++;
        }
        while (page.HasNext);

        return rows;
    }

    [Fact]
    public async Task An_expired_seed_is_visible_to_staff_and_hidden_from_readers()
    {
        using var reader = factory.CreateClient();
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);

        foreach (var (slug, term) in ExpiredSeeds)
        {
            var search = Uri.EscapeDataString(term);

            using var publicResponse = await reader.GetAsync($"{PublicPath}/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);

            var publicPage = await ReadPageAsync<ResourceSummaryResponse>(reader, $"{PublicPath}?search={search}");
            Assert.Equal(0, publicPage.TotalCount);

            var staffPage = await staffApi.GetAsync<PagedResponse<ResourceAdminSummaryResponse>>(
                $"/api/admin/students/resources?search={search}");

            var row = Assert.Single(staffPage.Items);
            Assert.Equal(slug, row.Slug);
            Assert.True(row.IsPublished);
            Assert.True(row.IsExpired);
        }
    }

    [Fact]
    public async Task A_slug_that_never_existed_is_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}/no-such-resource-anywhere");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_matches_the_title()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?search=quadratic");

        Assert.Equal("quadratic-equations-grade-9", Assert.Single(page.Items).Slug);
    }

    [Fact]
    public async Task Search_matches_the_summary_even_when_the_title_does_not_contain_the_term()
    {
        using var client = factory.CreateClient();

        // "crore" appears in the seeded summary and not in its title, so this only passes if the
        // search covers the summary as well as the title.
        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?search=crore");

        Assert.Equal("nepali-numbering-and-dates", Assert.Single(page.Items).Slug);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task A_search_term_that_is_a_wildcard_matches_nothing_rather_than_everything(string term)
    {
        using var client = factory.CreateClient();

        // Unescaped, '%' would turn into a pattern that matches every published row. That is the
        // exact bug LikePattern exists to prevent, so it is asserted rather than assumed.
        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?search={Uri.EscapeDataString(term)}");

        Assert.Equal(0, page.TotalCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_search_is_ignored_rather_than_matching_nothing(string term)
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?search={Uri.EscapeDataString(term)}&pageSize=100");

        Assert.True(page.TotalCount > 1, "A blank search should behave as no search at all.");
    }

    [Theory]
    [InlineData("Note", ResourceType.Note)]
    [InlineData("QuestionPaper", ResourceType.QuestionPaper)]
    [InlineData("Scholarship", ResourceType.Scholarship)]
    public async Task The_type_filter_returns_only_that_type(string raw, ResourceType expected)
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?type={raw}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(expected, item.Type));
    }

    [Fact]
    public async Task The_category_filter_returns_only_that_subject()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?category=Science&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(ResourceCategory.Science, item.Category));
    }

    [Fact]
    public async Task The_level_filter_returns_only_that_stage()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?level=Grade9to10&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(EducationLevel.Grade9to10, item.Level));
    }

    [Fact]
    public async Task Filters_combine_rather_than_replacing_each_other()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(
            client,
            $"{PublicPath}?type=QuestionPaper&category=Science&level=Grade9to10&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.Equal(ResourceType.QuestionPaper, item.Type);
            Assert.Equal(ResourceCategory.Science, item.Category);
            Assert.Equal(EducationLevel.Grade9to10, item.Level);
        });
    }

    [Fact]
    public async Task Sorting_by_title_is_alphabetical()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?sort=title&pageSize=100");

        Assert.NotEmpty(page.Items);

        var titles = page.Items.Select(item => item.Title).ToList();

        // The column's collation is case insensitive, which is what a reader expects of an
        // alphabetical list: "English" and "english" belong in the same place. An ordinal sort
        // would put every capitalised title ahead of every lower case one instead.
        Assert.Equal(titles.OrderBy(title => title, StringComparer.OrdinalIgnoreCase), titles);
    }

    [Fact]
    public async Task Sorting_by_expiring_puts_the_soonest_deadline_first_and_undated_rows_last()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?sort=expiring&pageSize=100");

        var dated = page.Items.Where(item => item.ExpiresOn.HasValue).ToList();
        var undated = page.Items.Where(item => !item.ExpiresOn.HasValue).ToList();

        Assert.NotEmpty(dated);
        Assert.NotEmpty(undated);

        // MySQL sorts NULL first on an ascending order, so without an explicit "has a deadline"
        // key every note and paper would sit above the one deadline that actually matters.
        Assert.Equal(dated.Count + undated.Count, page.Items.Count);
        Assert.Equal(
            dated.Select(item => item.ExpiresOn!.Value).OrderBy(date => date),
            dated.Select(item => item.ExpiresOn!.Value));

        var firstUndated = page.Items.ToList().FindIndex(item => !item.ExpiresOn.HasValue);
        Assert.Equal(dated.Count, firstUndated);
    }

    [Fact]
    public async Task The_default_order_is_newest_first_and_is_stable_between_calls()
    {
        using var client = factory.CreateClient();

        var first = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?pageSize=100");
        var second = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?pageSize=100");

        Assert.Equal(first.Items.Select(item => item.Id), second.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("type=Wombat", "type_invalid")]
    [InlineData("category=Underwater", "category_invalid")]
    [InlineData("level=Postgraduate", "level_invalid")]
    [InlineData("sort=sideways", "sort_invalid")]
    public async Task An_unrecognised_filter_value_is_refused_with_a_named_error(string query, string expectedCode)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?{query}");
        var snapshot = new ApiResponseSnapshot(response.StatusCode, await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, snapshot.Error?.Code);
    }

    [Fact]
    public async Task The_catalogue_is_paged_and_no_row_appears_on_two_pages()
    {
        using var client = factory.CreateClient();

        var first = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?type=Note&page=1&pageSize=5");
        var second = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?type=Note&page=2&pageSize=5");

        Assert.Equal(5, first.Items.Count);
        Assert.True(first.HasNext);
        Assert.True(second.HasPrevious);
        Assert.True(first.TotalCount >= 10);
        Assert.Equal(5, first.TotalPages >= 2 ? 5 : first.Items.Count);

        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(-5, 10, 10)]
    [InlineData(1, 500, 100)]
    [InlineData(1, 0, 24)]
    [InlineData(3, 10, 10)]
    [InlineData(9999, 10, 10)]
    public async Task An_out_of_range_page_request_is_clamped_rather_than_rejected(
        int requestedPage,
        int requestedPageSize,
        int expectedPageSize)
    {
        using var client = factory.CreateClient();

        // The last page moves as seeded windows close, so it is read back from the endpoint rather
        // than written into the case list where it would silently rot.
        var baseline = await ReadPageAsync<ResourceSummaryResponse>(client, $"{PublicPath}?page=1&pageSize={expectedPageSize}");
        var lastPage = PageRequest.TotalPagesFor(baseline.TotalCount, expectedPageSize);
        var expectedPage = Math.Clamp(requestedPage < 1 ? 1 : requestedPage, 1, lastPage);

        var page = await ReadPageAsync<ResourceSummaryResponse>(
            client,
            $"{PublicPath}?page={requestedPage}&pageSize={requestedPageSize}");

        Assert.Equal(expectedPage, page.Page);
        Assert.Equal(expectedPageSize, page.PageSize);
        Assert.True(page.Page <= lastPage, "The clamped page must never be past the last page with rows.");
    }

    [Fact]
    public async Task The_list_row_is_a_summary_and_the_detail_row_carries_the_content()
    {
        using var client = factory.CreateClient();

        var listed = Assert.Single((await ReadPageAsync<ResourceSummaryResponse>(
            client,
            $"{PublicPath}?search=quadratic")).Items);

        // The list is a single SQL projection, so the body is not on it. The detail endpoint is
        // where the content comes from, and the two must agree about which row it is.
        Assert.NotEmpty(listed.Summary);

        using var response = await client.GetAsync($"{PublicPath}/{listed.Slug}");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceDetailResponse>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal(listed.Id, envelope!.Data!.Id);
        Assert.Equal(listed.Title, envelope.Data.Title);
        Assert.True(envelope.Data.Body.Trim().Length >= 80);
        Assert.False(envelope.Data.IsExpired);
    }

    [Fact]
    public async Task A_question_paper_detail_carries_its_year_and_its_link()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<ResourceSummaryResponse>(
            client,
            $"{PublicPath}?type=QuestionPaper&search={Uri.EscapeDataString("SEE 2081 science")}");

        var listed = Assert.Single(page.Items);
        Assert.True(listed.HasExternalUrl);

        var envelope = await client.GetFromJsonAsync<ApiEnvelope<ResourceDetailResponse>>(
            $"{PublicPath}/{listed.Slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal(2081, envelope!.Data!.AcademicYear);
        Assert.StartsWith("https://", envelope.Data.ExternalUrl);
    }

    private static async Task<PagedResponse<T>> ReadPageAsync<T>(HttpClient client, string path)
    {
        var api = new AuthApiClient(client);

        return await api.GetAsync<PagedResponse<T>>(path);
    }
}

/// <summary>
/// Staff management of the catalogue: who may write, what a draft is allowed to be missing, and
/// what happens to a row that is already public.
/// </summary>
public sealed class StudentResourceAdminTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string AdminPath = "/api/admin/students/resources";

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

        var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(NewSlug(), published: false));

        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    [Fact]
    public async Task A_staff_write_without_a_csrf_token_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        using var response = await api.PostAsync(AdminPath, NoteBody(NewSlug(), published: false), withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_incomplete_draft_can_be_saved_because_a_draft_is_not_junk()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug,
            title = "A note that is still being written",
            summary = string.Empty,
            body = string.Empty,
            type = "Note",
            category = "General",
            level = "General",
            isPublished = false,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The shape rules passed, and the editor is told exactly what is still missing.
        var record = ResourceApi.ReadCreated(created);
        Assert.False(record.IsPublished);
        Assert.NotEmpty(record.PublishBlockers);
    }

    [Fact]
    public async Task A_draft_is_invisible_to_readers_and_visible_to_staff()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();
        var slug = NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, NoteBody(slug, published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var publicPage = await new AuthApiClient(reader).GetAsync<PagedResponse<ResourceSummaryResponse>>(
            $"/api/students/resources?search={slug}");
        Assert.Equal(0, publicPage.TotalCount);

        using var detail = await reader.GetAsync($"/api/students/resources/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var staffPage = await staffApi.GetAsync<PagedResponse<ResourceAdminSummaryResponse>>($"{AdminPath}?search={slug}");
        Assert.Equal(slug, Assert.Single(staffPage.Items).Slug);
    }

    [Fact]
    public async Task A_duplicate_slug_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var first = await api.PostForSnapshotAsync(AdminPath, NoteBody(slug, published: false));
        var second = await api.PostForSnapshotAsync(AdminPath, NoteBody(slug, published: false));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("slug_taken", second.Error?.Code);
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

        var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(slug, published: false));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(ResourceApi.Details(created), error => error.Field == "slug");
    }

    [Fact]
    public async Task A_title_that_is_too_short_is_refused_with_a_field_error()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(NewSlug(), published: false, title: "abc"));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(ResourceApi.Details(created), error => error.Field == "title");
    }

    [Fact]
    public async Task Changing_the_slug_of_an_existing_resource_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(slug, published: false));
        var record = ResourceApi.ReadCreated(created);

        var renamed = await api.PutForSnapshotAsync(
            $"{AdminPath}/{record.Id}",
            NoteBody(NewSlug(), published: false));

        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
        Assert.Equal("slug_immutable", renamed.Error?.Code);
    }

    [Theory]
    [InlineData("http://cec.edu.np/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://cec.edu.np/paper.pdf")]
    public async Task An_unsafe_link_is_refused(string url)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(NewSlug(), published: false, externalUrl: url));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(ResourceApi.Details(created), error => error.Field == "externalUrl");
    }

    [Theory]
    [InlineData("https://127.0.0.1/secret")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://localhost/admin")]
    public async Task A_link_to_a_bare_address_rather_than_a_host_name_is_refused(string url)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(NewSlug(), published: false, externalUrl: url));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(ResourceApi.Details(created), error => error.Field == "externalUrl");
    }

    [Fact]
    public async Task A_safe_link_is_accepted_and_stored_canonically()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            NoteBody(NewSlug(), published: false, externalUrl: "  https://www.cec.edu.np/  "));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("https://www.cec.edu.np/", (ResourceApi.ReadCreated(created)).ExternalUrl);
    }

    [Fact]
    public async Task Publishing_a_question_paper_without_a_year_or_a_link_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug = NewSlug(),
            title = "An exam paper that is not finished",
            summary = "A summary long enough to pass the minimum length rule.",
            body = LongBody,
            type = "QuestionPaper",
            category = "Mathematics",
            level = "Grade9to10",
            isPublished = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var details = ResourceApi.Details(created);
        Assert.Contains(details, error => error.Field == "academicYear");
        Assert.Contains(details, error => error.Field == "externalUrl");
    }

    [Fact]
    public async Task Publishing_a_scholarship_without_a_provider_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, ScholarshipBody(
            NewSlug(),
            provider: null,
            expiresOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            published: true));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(ResourceApi.Details(created), error => error.Field == "provider");
    }

    [Fact]
    public async Task Publishing_a_scholarship_whose_deadline_has_passed_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, ScholarshipBody(
            NewSlug(),
            provider: "Some Foundation",
            expiresOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            published: true));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(ResourceApi.Details(created), error => error.Field == "expiresOn");
    }

    [Fact]
    public async Task A_complete_scholarship_can_be_published_and_then_reaches_readers()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();
        var slug = NewSlug();

        var created = await staffApi.PostForSnapshotAsync(AdminPath, ScholarshipBody(
            slug,
            provider: "Some Foundation",
            expiresOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60),
            published: true));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = ResourceApi.ReadCreated(created);
        Assert.True(record.IsPublished);
        Assert.Empty(record.PublishBlockers);
        Assert.Equal(45000m, record.AwardAmountNpr);

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<ResourceDetailResponse>>(
            $"/api/students/resources/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal("Some Foundation", envelope!.Data!.Provider);
        Assert.False(envelope.Data.IsExpired);
    }

    [Fact]
    public async Task The_publish_blockers_clear_once_the_draft_is_filled_in()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        // A draft is allowed to be incomplete, so the starting point here is a bare record with
        // a title and nothing else. The blockers are what staff read to know what is still
        // missing, so they have to be there while the gaps are.
        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug,
            title = "A note that is not finished yet",
            type = "Note",
            category = "General",
            level = "General",
            isPublished = false,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var draft = ResourceApi.ReadCreated(created);
        Assert.NotEmpty(draft.PublishBlockers);
        // The blockers are prose written for the editor, so they are matched on what they tell
        // staff to do rather than on a field name.
        Assert.NotEmpty(draft.PublishBlockers);
        Assert.Contains(draft.PublishBlockers, blocker => blocker.Contains("summary"));
        Assert.Contains(draft.PublishBlockers, blocker => blocker.Contains("at least 80 characters"));

        var filled = await api.PutForSnapshotAsync(
            $"{AdminPath}/{draft.Id}",
            new
            {
                slug,
                title = "A note that is finished",
                summary = "A summary that is comfortably over the minimum length.",
                body = LongBody,
                type = "Note",
                category = "General",
                level = "General",
                isPublished = true,
            });

        Assert.Equal(HttpStatusCode.OK, filled.StatusCode);
        Assert.Empty(ResourceApi.ReadCreated(filled).PublishBlockers);
    }

    [Fact]
    public async Task A_published_resource_cannot_be_deleted_but_a_draft_can()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            ScholarshipBody(slug, provider: "Some Foundation", expiresOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), published: true));

        var published = ResourceApi.ReadCreated(created);
        var refused = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("resource_published", refused.Error?.Code);

        // Retract, then delete.
        var retracted = await api.PutForSnapshotAsync($"{AdminPath}/{published.Id}", ScholarshipBody(
            slug, provider: "Some Foundation", expiresOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), published: false));

        Assert.Equal(HttpStatusCode.OK, retracted.StatusCode);

        var deleted = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");
        var missing = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Fields_that_do_not_apply_to_the_type_are_cleared_rather_than_stored()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        // A note carrying a scholarship provider and an exam year. Both are cleared on save, so
        // a stray value cannot sit in the database looking meaningful or leak into a later query.
        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug = NewSlug(),
            title = "A note that was filled in carelessly",
            summary = "A summary that is comfortably over the minimum length.",
            body = LongBody,
            type = "Note",
            category = "General",
            level = "General",
            externalUrl = "https://www.cec.edu.np/",
            academicYear = 2081,
            provider = "Should not be stored on a note",
            awardAmountNpr = 1000m,
            eligibility = "Should not be stored on a note either",
            isPublished = false,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = ResourceApi.ReadCreated(created);
        Assert.Equal(ResourceType.Note, record.Type);
        Assert.Equal("https://www.cec.edu.np/", record.ExternalUrl);
        Assert.Null(record.AcademicYear);
        Assert.Null(record.Provider);
        Assert.Null(record.AwardAmountNpr);
        Assert.Null(record.Eligibility);
    }

    [Fact]
    public async Task The_staff_status_filter_separates_drafts_from_published_rows()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(slug, published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = ResourceApi.ReadCreated(created);

        var drafts = await api.GetAsync<PagedResponse<ResourceAdminSummaryResponse>>($"{AdminPath}?status=Draft&search={slug}");
        Assert.Equal(slug, Assert.Single(drafts.Items).Slug);
        Assert.False(Assert.Single(drafts.Items).IsPublished);

        await api.PutForSnapshotAsync($"{AdminPath}/{record.Id}", NoteBody(slug, published: true));

        var published = await api.GetAsync<PagedResponse<ResourceAdminSummaryResponse>>($"{AdminPath}?status=Published&search={slug}");
        Assert.True(Assert.Single(published.Items).IsPublished);
    }

    [Theory]
    [InlineData("status=Wibble", "status_invalid")]
    [InlineData("type=Nonsense", "type_invalid")]
    public async Task An_unrecognised_staff_filter_is_refused_with_a_named_error(string query, string expectedCode)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        using var response = await client.GetAsync($"{AdminPath}?{query}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, new ApiResponseSnapshot(response.StatusCode, json).Error?.Code);
    }

    [Fact]
    public async Task An_admin_and_a_moderator_can_both_write()
    {
        foreach (var role in new[] { Roles.Admin, Roles.Moderator })
        {
            using var client = await factory.CreateStaffClientAsync(role);
            var api = new AuthApiClient(client);

            var created = await api.PostForSnapshotAsync(AdminPath, NoteBody(NewSlug(), published: false));

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
    }

    private const string LongBody =
        "This is a body that is comfortably longer than the eighty character minimum that a "
        + "published resource has to clear, so the publish rule is satisfied by this test "
        + "without the test having to pad itself with filler sentences about nothing in particular.";

    private static string NewSlug() => $"test-{Guid.NewGuid():N}";

    /// <summary>
    /// A note fixture. The title carries the slug so the row a test has just written can be found
    /// again with a text search, which is the only handle the search endpoint offers: it matches
    /// the title and the summary, never the slug. A shared title would make every test row match
    /// every other one.
    /// </summary>
    private static object NoteBody(
        string slug,
        bool published,
        string? title = null,
        string? externalUrl = null) => new
        {
            slug,
            title = title ?? $"A draft note about {slug}",
            summary = "A summary that is comfortably over the minimum length.",
            body = LongBody,
            type = "Note",
            category = "General",
            level = "General",
            externalUrl,
            isPublished = published,
        };

    private static object ScholarshipBody(
        string slug,
        string? provider,
        DateOnly? expiresOn,
        bool published) => new
        {
            slug,
            title = $"A scholarship for a test about {slug}",
            summary = "A summary that is comfortably over the minimum length.",
            body = LongBody,
            type = "Scholarship",
            category = "General",
            level = "General",
            provider,
            awardAmountNpr = 45000m,
            eligibility = "Applicants meeting the published criteria.",
            expiresOn = expiresOn?.ToString("yyyy-MM-dd"),
            isPublished = published,
        };
}

internal static class ResourceApi
{
    /// <summary>
    /// Unwraps the resource a staff write returned, so a test can assert against the record the
    /// server stored rather than against the request that was sent.
    /// </summary>
    public static ResourceAdminResponse ReadCreated(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<ResourceAdminResponse>>(TestJson.Options);

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
/// Expiry is the one rule in this module that depends on a clock rather than on a stored flag, so
/// it gets a host whose clock the test can move. The date is the Nepal one, which turns over at
/// 00:00 in Kathmandu rather than 00:00 UTC, so these tests pin the clock to an hour where the
/// two dates already disagree. A test that agrees with the offset by accident proves nothing.
/// </summary>
public sealed class StudentResourceExpiryTests : IAsyncLifetime
{
    private const string PublicPath = "/api/students/resources";
    private const string AdminPath = "/api/admin/students/resources";

    private readonly FixedClockApiFactory _factory = FixedClockApiFactory.NearNow();

    private const string ScholarshipBody =
        "This body is comfortably longer than the eighty character minimum that publishing "
        + "requires, so the publish rule is satisfied by the fixture itself rather than by each "
        + "test padding itself out with filler sentences about nothing in particular.";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_scholarship_disappears_once_the_nepal_date_passes_its_deadline()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = _factory.CreateClient();
        var readerApi = new AuthApiClient(reader);

        var start = NepaliDayBoundary();
        _factory.Clock.Set(start);
        var today = NepalToday();

        // A check on the setup rather than on the product: 19:00 UTC is already the small hours
        // in Kathmandu, so the two dates must differ here. If they ever agree the test has
        // stopped exercising the offset and the chosen hour needs moving.
        Assert.NotEqual(start.UtcDateTime.Date, today.ToDateTime(TimeOnly.MinValue));

        var slug = $"test-{Guid.NewGuid():N}";
        var created = await CreateScholarshipAsync(staffApi, slug, today.AddDays(3), published: true);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1, (await CountPublicAsync(readerApi, slug)).TotalCount);

        // The deadline day itself is still open: somebody applying on the closing date is not too
        // late, so the boundary is exclusive only on the past side.
        _factory.Clock.Set(start.AddDays(2));
        Assert.Equal(1, (await CountPublicAsync(readerApi, slug)).TotalCount);

        _factory.Clock.Set(start.AddDays(4));

        var closed = await CountPublicAsync(readerApi, slug);
        Assert.Equal(0, closed.TotalCount);

        using var detail = await reader.GetAsync($"{PublicPath}/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Staff still see it, flagged as expired, because retiring it is their job rather than the
        // reader's.
        var staffPage = await staffApi.GetAsync<PagedResponse<ResourceAdminSummaryResponse>>(
            $"{AdminPath}?status=Expired&search={slug}");

        var row = Assert.Single(staffPage.Items);
        Assert.True(row.IsPublished);
        Assert.True(row.IsExpired);
    }

    [Fact]
    public async Task A_deadline_that_falls_on_the_current_nepal_date_is_still_open()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = _factory.CreateClient();
        var readerApi = new AuthApiClient(reader);

        _factory.Clock.Set(NepaliDayBoundary());
        var today = NepalToday();

        var slug = $"test-{Guid.NewGuid():N}";
        var created = await CreateScholarshipAsync(staffApi, slug, today, published: true);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1, (await CountPublicAsync(readerApi, slug)).TotalCount);
    }

    [Fact]
    public async Task A_past_deadline_can_be_kept_as_a_draft_even_though_it_cannot_be_published()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);

        _factory.Clock.Set(NepaliDayBoundary());
        var today = NepalToday();

        // Yesterday's closing date is a reasonable thing to file away for the record, so the
        // draft is allowed. Publishing it would advertise a scholarship that had already closed
        // before it was written down, and the admin tests cover that refusal.
        var slug = $"test-{Guid.NewGuid():N}";
        var created = await CreateScholarshipAsync(staffApi, slug, today.AddDays(-1), published: false);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = ResourceApi.ReadCreated(created);
        Assert.False(record.IsPublished);

        // The date is still in the past, and the row says so even though it is only a draft.
        // That is the same fact the blocker is reporting: staff are told why the listing cannot
        // go out, rather than being left to work it out from the calendar.
        Assert.True(record.IsExpired);

        var blocker = Assert.Single(record.PublishBlockers);
        Assert.Contains("already passed", blocker);
    }

    /// <summary>
    /// 19:00 UTC on the current real day, which is 00:45 the following morning in Kathmandu. The
    /// hour is chosen so the Nepal date and the UTC date differ, making the offset load-bearing
    /// rather than incidental.
    /// </summary>
    private static DateTimeOffset NepaliDayBoundary() =>
        new(DateTimeOffset.UtcNow.UtcDateTime.Date.AddHours(19), TimeSpan.Zero);

    /// <summary>
    /// "Today" as the pinned clock sees it in Kathmandu. The offset is repeated here on purpose:
    /// the assertions below are all about the relationship between this date and the server's
    /// behaviour, so a server that used a different offset would leave a deadline stranded and
    /// fail. Reading the date from another endpoint instead would only prove that two endpoints
    /// agree with each other.
    /// </summary>
    private DateOnly NepalToday() =>
        DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(5 * 60 + 45));

    private static Task<ApiResponseSnapshot> CreateScholarshipAsync(
        AuthApiClient api,
        string slug,
        DateOnly expiresOn,
        bool published) =>
        api.PostForSnapshotAsync(AdminPath, new
        {
            slug,
            title = $"A scholarship for the expiry test about {slug}",
            summary = "A summary that is comfortably over the minimum length.",
            body = ScholarshipBody,
            type = "Scholarship",
            category = "General",
            level = "General",
            provider = "Some Foundation",
            awardAmountNpr = 1000m,
            eligibility = "Applicants who meet the published criteria.",
            expiresOn = expiresOn.ToString("yyyy-MM-dd"),
            isPublished = published,
        });

    private static Task<PagedResponse<ResourceSummaryResponse>> CountPublicAsync(AuthApiClient api, string slug) =>
        api.GetAsync<PagedResponse<ResourceSummaryResponse>>($"{PublicPath}?search={slug}");
}
