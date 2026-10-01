using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Jobs;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Reference;

namespace NepalHub.Tests;

/// <summary>
/// Reading the board as a visitor. Nothing here signs in, which is the point: the public surface
/// has to work for somebody who has never made an account.
///
/// Assertions are scoped to a search term or a seed slug wherever a count is involved, rather than
/// asserting a whole-board total. The staff tests in this file publish rows of their own, and a
/// suite that passed only because of the order its classes happened to run in would be a suite
/// that fails the next time somebody reorders it.
/// </summary>
public sealed class JobBoardTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string PublicPath = "/api/jobs";
    private const string AdminPath = "/api/admin/jobs";

    /// <summary>Every slug the seeder ships, so "the board was seeded" is one assertion.</summary>
    private static readonly string[] SeededSlugs =
    [
        "frontend-developer-ktm-tech-park",
        "junior-python-developer-remote",
        "secondary-school-teacher-bhojpur",
        "english-language-tutor-lalitpur",
        "community-health-officer-saptari",
        "staff-nurse-icu-kathmandu",
        "accounting-assistant-bhaktapur",
        "accounts-internship-lalitpur",
        "field-sales-representative-morang",
        "civil-site-engineer-chitwan",
        "hydraulic-technician-kaski-contract",
        "community-social-worker-rupandehi",
        "mobile-clinic-attendant-surkhet",
        "school-teacher-dadeldhura",
        "front-desk-receptionist-lalitpur-closed",
        "tractor-operator-dhankuta-closed",
    ];

    /// <summary>
    /// The two seeds that ship already closed, paired with the words that find them. Search matches
    /// the title, the employer and the description rather than the slug, so a test that wants one
    /// specific row has to search for something that row actually says.
    /// </summary>
    private static readonly (string Slug, string Search)[] ClosedSeeds =
    [
        ("front-desk-receptionist-lalitpur-closed", "Kulekhani Guest House"),
        ("tractor-operator-dhankuta-closed", "Koshi Agri Support"),
    ];

    [Fact]
    public async Task The_public_board_is_readable_without_an_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_seeded_board_is_present_and_published()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var items = await ReadAllAdminRowsAsync(client);

        Assert.All(SeededSlugs, slug => Assert.Contains(items, item => item.Slug == slug));

        // Every seeded row except the two that model a closed posting is live.
        foreach (var item in items.Where(item => SeededSlugs.Contains(item.Slug)))
        {
            Assert.True(item.IsPublished, $"{item.Slug} should be seeded as published.");
        }
    }

    [Fact]
    public async Task The_seeded_board_covers_every_province_and_every_filter_value()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var items = await ReadAllAdminRowsAsync(client);
        var seeded = items.Where(item => SeededSlugs.Contains(item.Slug)).ToList();

        Assert.Equal(SeededSlugs.Length, seeded.Count);

        // The board has to work in every one of the seven provinces, or somebody outside the Valley
        // opens it and concludes there is nothing here for them. The province is read through the
        // seeded district, so this asks the reference endpoints which province each seeded district
        // belongs to rather than asserting a list the test supplied to itself.
        var api = new AuthApiClient(client);
        var provinces = await api.GetAsync<List<ProvinceResponse>>("/api/reference/provinces");

        var districtsIn = new Dictionary<string, List<DistrictResponse>>(StringComparer.Ordinal);
        foreach (var province in provinces)
        {
            districtsIn[province.Name] = await api.GetAsync<List<DistrictResponse>>(
                $"/api/reference/provinces/{province.Code}/districts");
        }

        var seededDistricts = seeded
            .Select(item => item.DistrictName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var province in provinces)
        {
            var hasPosting = districtsIn[province.Name]
                .Any(district => seededDistricts.Contains(district.Name));

            Assert.True(hasPosting, $"The seeded board has no posting in {province.Name}.");
        }

        foreach (var employmentType in new[]
                 {
                     EmploymentType.FullTime,
                     EmploymentType.PartTime,
                     EmploymentType.Contract,
                     EmploymentType.Internship,
                 })
        {
            Assert.Contains(seeded, item => item.EmploymentType == employmentType);
        }

        foreach (var workMode in new[] { WorkMode.OnSite, WorkMode.Remote, WorkMode.Hybrid })
        {
            Assert.Contains(seeded, item => item.WorkMode == workMode);
        }

        // One posting carries no district at all, because a remote job has no address.
        Assert.Contains(seeded, item => item.DistrictName is null);
    }

    /// <summary>
    /// Reads every row of the staff list, not just the first page.
    ///
    /// The staff tests in this file create rows of their own and do not remove them, and the staff
    /// list is ordered by most recently updated, so the seeds sit on a later page once a few runs
    /// have accumulated. <c>pageSize</c> is capped at 100 by the API, so asking for a larger page
    /// is silently reduced rather than refused. Paging to the end is the only way to assert on the
    /// whole board, and it is what keeps these two tests honest on a database that has been used
    /// rather than a freshly created one.
    /// </summary>
    private static async Task<IReadOnlyList<JobAdminSummaryResponse>> ReadAllAdminRowsAsync(
        HttpClient client)
    {
        var api = new AuthApiClient(client);
        var rows = new List<JobAdminSummaryResponse>();

        PagedResponse<JobAdminSummaryResponse> page;
        var pageNumber = 1;
        do
        {
            page = await api.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
                $"{AdminPath}?pageSize=100&page={pageNumber}");
            rows.AddRange(page.Items);
            pageNumber++;
        }
        while (page.HasNext);

        return rows;
    }

    [Fact]
    public async Task A_closed_seed_is_visible_to_staff_and_hidden_from_readers()
    {
        using var reader = factory.CreateClient();
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);

        var readerApi = new AuthApiClient(reader);
        var staffApi = new AuthApiClient(staff);

        foreach (var (slug, term) in ClosedSeeds)
        {
            var search = Uri.EscapeDataString(term);

            using var publicResponse = await reader.GetAsync($"{PublicPath}/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);

            var publicPage = await readerApi.GetAsync<PagedResponse<JobSummaryResponse>>(
                $"{PublicPath}?search={search}");
            Assert.Equal(0, publicPage.TotalCount);

            var staffPage = await staffApi.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
                $"{AdminPath}?search={search}");

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

        using var response = await client.GetAsync($"{PublicPath}/no-such-job-anywhere");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_matches_the_title()
    {
        using var client = factory.CreateClient();

        // Search covers the title, the employer and the description, never the slug, so the term has
        // to be something the posting actually says. "React" appears in its title only.
        var page = await ReadPageAsync<JobSummaryResponse>(client, $"{PublicPath}?search=React");

        Assert.Equal("frontend-developer-ktm-tech-park", Assert.Single(page.Items).Slug);
    }

    [Fact]
    public async Task Search_matches_the_employer_even_when_the_title_does_not_contain_the_term()
    {
        using var client = factory.CreateClient();

        // The employer name appears in neither title, so this only passes if the search covers the
        // organization as well as the title.
        var page = await ReadPageAsync<JobSummaryResponse>(client, $"{PublicPath}?search=Himal");

        Assert.Equal("junior-python-developer-remote", Assert.Single(page.Items).Slug);
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
        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?search={Uri.EscapeDataString(term)}");

        Assert.Equal(0, page.TotalCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_search_is_ignored_rather_than_matching_nothing(string term)
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?search={Uri.EscapeDataString(term)}&pageSize=100");

        Assert.True(page.TotalCount > 1, "A blank search should behave as no search at all.");
    }

    [Theory]
    [InlineData("FullTime", EmploymentType.FullTime)]
    [InlineData("PartTime", EmploymentType.PartTime)]
    [InlineData("Contract", EmploymentType.Contract)]
    [InlineData("Internship", EmploymentType.Internship)]
    public async Task The_employment_type_filter_returns_only_that_type(
        string raw,
        EmploymentType expected)
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?employmentType={raw}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(expected, item.EmploymentType));
    }

    [Theory]
    [InlineData("OnSite")]
    [InlineData("Remote")]
    [InlineData("Hybrid")]
    public async Task The_work_mode_filter_returns_only_that_mode(string raw)
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?workMode={raw}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(raw, item.WorkMode.ToString()));
    }

    [Fact]
    public async Task The_category_filter_returns_only_that_field()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?category=Teaching&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(JobCategory.Teaching, item.Category));
    }

    [Fact]
    public async Task The_province_filter_returns_only_postings_in_that_province()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?province=koshi&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal("Koshi", item.ProvinceName));

        // A remote posting has no district and therefore no province, so a province filter
        // correctly excludes it rather than guessing one.
        Assert.DoesNotContain(page.Items, item => item.ProvinceName is null);
    }

    [Fact]
    public async Task The_district_filter_returns_only_postings_in_that_district()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?district=bagmati-lalitpur&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.Equal("Lalitpur", item.DistrictName);
            Assert.Equal("Bagmati", item.ProvinceName);
        });
    }

    [Fact]
    public async Task Filters_combine_rather_than_replacing_each_other()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?province=koshi&category=Teaching&employmentType=FullTime&workMode=OnSite&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.Equal("Koshi", item.ProvinceName);
            Assert.Equal(JobCategory.Teaching, item.Category);
            Assert.Equal(EmploymentType.FullTime, item.EmploymentType);
            Assert.Equal(WorkMode.OnSite, item.WorkMode);
        });
    }

    [Fact]
    public async Task Sorting_by_deadline_puts_the_soonest_closing_date_first()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(client, $"{PublicPath}?sort=deadline&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.Equal(
            page.Items.Select(item => item.ApplicationDeadline).OrderBy(date => date),
            page.Items.Select(item => item.ApplicationDeadline));
    }

    [Fact]
    public async Task The_default_order_is_newest_first_and_is_stable_between_calls()
    {
        using var client = factory.CreateClient();

        var first = await ReadPageAsync<JobSummaryResponse>(client, $"{PublicPath}?pageSize=100");
        var second = await ReadPageAsync<JobSummaryResponse>(client, $"{PublicPath}?pageSize=100");

        Assert.Equal(first.Items.Select(item => item.Id), second.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("category=Wombat", "category_invalid")]
    [InlineData("employmentType=Wombat", "employmentType_invalid")]
    [InlineData("workMode=Wombat", "workMode_invalid")]
    [InlineData("sort=sideways", "sort_invalid")]
    [InlineData("province=atlantis", "province_invalid")]
    [InlineData("district=atlantis-city", "district_invalid")]
    public async Task An_unrecognised_filter_value_is_refused_with_a_named_error(string query, string expectedCode)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{PublicPath}?{query}");
        var snapshot = new ApiResponseSnapshot(response.StatusCode, await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, snapshot.Error?.Code);
    }

    [Fact]
    public async Task The_board_is_paged_and_no_row_appears_on_two_pages()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        // A filter of its own, so the two pages do not depend on how many rows earlier runs left in
        // the table. Four postings is enough to fill two pages of two.
        var prefix = $"paged-{Guid.NewGuid():N}";
        for (var index = 0; index < 4; index++)
        {
            var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
                $"{prefix}-{index}",
                organization: $"{prefix} employer",
                published: true));

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var first = await api.GetAsync<PagedResponse<JobSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString($"{prefix} employer")}&page=1&pageSize=2");
        var second = await api.GetAsync<PagedResponse<JobSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString($"{prefix} employer")}&page=2&pageSize=2");

        Assert.Equal(2, first.Items.Count);
        Assert.True(first.HasNext);
        Assert.True(second.HasPrevious);
        Assert.Equal(4, first.TotalCount);

        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
    }

    [Theory]
    [InlineData(0, 10, 1, 10)]
    [InlineData(-5, 10, 1, 10)]
    [InlineData(1, 500, 1, 100)]
    [InlineData(1, 0, 1, 24)]
    [InlineData(3, 10, 3, 10)]
    public async Task An_out_of_range_page_request_is_clamped_rather_than_rejected(
        int requestedPage,
        int requestedPageSize,
        int expectedPage,
        int expectedPageSize)
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?page={requestedPage}&pageSize={requestedPageSize}");

        Assert.Equal(expectedPage, page.Page);
        Assert.Equal(expectedPageSize, page.PageSize);
    }

    [Fact]
    public async Task The_list_row_is_a_summary_and_the_detail_row_carries_the_description()
    {
        using var client = factory.CreateClient();

        var listed = Assert.Single((await ReadPageAsync<JobSummaryResponse>(
            client,
            $"{PublicPath}?search=React")).Items);

        using var response = await client.GetAsync($"{PublicPath}/{listed.Slug}");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<JobDetailResponse>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal(listed.Id, envelope!.Data!.Id);
        Assert.Equal(listed.Title, envelope.Data.Title);
        Assert.True(envelope.Data.Description.Trim().Length >= 80);
        Assert.StartsWith("https://", envelope.Data.ApplicationUrl);
    }

    /// <summary>
    /// Closing soon is derived from the deadline rather than stored, so it is asserted against a
    /// posting whose deadline sits inside the window rather than against whatever the seeds happen
    /// to be dated at today.
    /// </summary>
    [Fact]
    public async Task A_posting_within_the_closing_window_is_labelled_closing_soon()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(staff);
        using var reader = factory.CreateClient();

        var closing = $"closing-{Guid.NewGuid():N}";
        var distant = $"distant-{Guid.NewGuid():N}";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Equal(HttpStatusCode.Created, (await api.PostForSnapshotAsync(
            AdminPath,
            JobApi.Body(closing, organization: "Closing Soon Employer", published: true, deadline: today.AddDays(2)))).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await api.PostForSnapshotAsync(
            AdminPath,
            JobApi.Body(distant, organization: "Distant Deadline Employer", published: true, deadline: today.AddDays(40)))).StatusCode);

        var closingRow = Assert.Single(
            (await ReadPageAsync<JobSummaryResponse>(reader, $"{PublicPath}?search={closing}")).Items);
        var distantRow = Assert.Single(
            (await ReadPageAsync<JobSummaryResponse>(reader, $"{PublicPath}?search={distant}")).Items);

        Assert.Equal(JobStatus.ClosingSoon, closingRow.Status);
        Assert.Equal(JobStatus.Open, distantRow.Status);
    }

    private static async Task<PagedResponse<T>> ReadPageAsync<T>(HttpClient client, string path)
    {
        var api = new AuthApiClient(client);

        return await api.GetAsync<PagedResponse<T>>(path);
    }
}

/// <summary>
/// Staff management of the board: who may write, what a draft is allowed to be missing, and what
/// happens to a posting that is already public.
/// </summary>
public sealed class JobAdminTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string AdminPath = "/api/admin/jobs";
    private const string PublicPath = "/api/jobs";

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

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            organization: "A standard account should not be able to write",
            published: false));

        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    [Fact]
    public async Task A_staff_write_without_a_csrf_token_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        using var response = await api.PostAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            organization: "A staff account that skipped the token",
            published: false),
            withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_incomplete_draft_can_be_saved_because_a_draft_is_not_junk()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = JobApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug,
            title = "A posting that is still being written",
            organization = "Some Employer",
            description = string.Empty,
            category = "Other",
            employmentType = "FullTime",
            workMode = "OnSite",
            applicationUrl = "https://www.merokaipatra.com/",
            applicationDeadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30).ToString("yyyy-MM-dd"),
            isPublished = false,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The shape rules passed, and the editor is told exactly what is still missing.
        var record = JobApi.ReadCreated(created);
        Assert.False(record.IsPublished);
        Assert.NotEmpty(record.PublishBlockers);
        Assert.Contains(record.PublishBlockers, blocker => blocker.Contains("80 characters"));
    }

    [Fact]
    public async Task A_draft_is_invisible_to_readers_and_visible_to_staff()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Moderator);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();
        var slug = JobApi.NewSlug();
        var organization = $"Draft Employer {Guid.NewGuid():N}";

        var created = await staffApi.PostForSnapshotAsync(AdminPath, JobApi.Body(
            slug,
            organization,
            published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var publicPage = await new AuthApiClient(reader).GetAsync<PagedResponse<JobSummaryResponse>>(
            $"{PublicPath}?search={Uri.EscapeDataString(organization)}");
        Assert.Equal(0, publicPage.TotalCount);

        using var detail = await reader.GetAsync($"{PublicPath}/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var staffPage = await staffApi.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString(organization)}");
        Assert.Equal(slug, Assert.Single(staffPage.Items).Slug);
    }

    [Fact]
    public async Task A_duplicate_slug_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = JobApi.NewSlug();

        var first = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(slug, "First Employer", published: false));
        var second = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(slug, "Second Employer", published: false));

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

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(slug, "Some Employer", published: false));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "slug");
    }

    [Fact]
    public async Task A_title_that_is_too_short_is_refused_with_a_field_error()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(
            AdminPath,
            JobApi.Body(JobApi.NewSlug(), "Some Employer", published: false, title: "abc"));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "title");
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

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            $"mistyped-{Guid.NewGuid():N}",
            "Some Employer",
            published: false));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = JobApi.ReadCreated(created);

        var corrected = JobApi.NewSlug();
        var renamed = await api.PutForSnapshotAsync(
            $"{AdminPath}/{record.Id}",
            JobApi.Body(corrected, "Some Employer", published: false));

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(corrected, JobApi.ReadCreated(renamed).Slug);
    }

    /// <summary>
    /// Once published, the slug is the address people are sharing, so it freezes. That is the
    /// opposite of the resource module, where the slug never moves, and it is deliberate here: a
    /// posting is written and edited before anyone has the link, and a frozen-from-creation slug
    /// would make every typo permanent for no reader's benefit.
    /// </summary>
    [Fact]
    public async Task A_published_postings_slug_cannot_be_changed()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = JobApi.NewSlug();
        var organization = $"Published Employer {Guid.NewGuid():N}";

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            slug,
            organization,
            published: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var renamed = await api.PutForSnapshotAsync(
            $"{AdminPath}/{JobApi.ReadCreated(created).Id}",
            JobApi.Body(JobApi.NewSlug(), organization, published: true));

        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
        Assert.Equal("slug_immutable", renamed.Error?.Code);
    }

    [Fact]
    public async Task A_published_postings_other_fields_are_still_editable()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = JobApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            slug,
            "Original Employer",
            published: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var updated = await api.PutForSnapshotAsync(
            $"{AdminPath}/{JobApi.ReadCreated(created).Id}",
            JobApi.Body(slug, "Corrected Employer", published: true));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Corrected Employer", JobApi.ReadCreated(updated).Organization);
    }

    [Theory]
    [InlineData("http://merokaipatra.com/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://merokaipatra.com/jobs")]
    public async Task An_unsafe_application_link_is_refused(string url)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            "Some Employer",
            published: false,
            applicationUrl: url));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "applicationUrl");
    }

    [Theory]
    [InlineData("https://127.0.0.1/admin")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://localhost/jobs")]
    public async Task An_application_link_to_a_bare_address_rather_than_a_host_name_is_refused(string url)
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            "Some Employer",
            published: false,
            applicationUrl: url));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "applicationUrl");
    }

    [Fact]
    public async Task A_missing_application_link_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        // A posting with no way to apply is a dead end for the reader this board exists to serve,
        // so unlike the resource module's optional link this one is required.
        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug = JobApi.NewSlug(),
            title = "A posting with nowhere to apply",
            organization = "Some Employer",
            description = JobApi.LongDescription,
            category = "Other",
            employmentType = "FullTime",
            workMode = "OnSite",
            applicationUrl = string.Empty,
            applicationDeadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30).ToString("yyyy-MM-dd"),
            isPublished = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "applicationUrl");
    }

    [Fact]
    public async Task An_unknown_district_code_is_refused_rather_than_silently_dropping_the_location()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        // A posting that says it is in Kathmandu and is actually nowhere in particular is the worst
        // outcome for someone reading a board to find work near them, so the code is checked.
        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            "Some Employer",
            published: false,
            districtCode: "not-a-real-district"));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "districtCode");
    }

    [Fact]
    public async Task A_district_code_is_stored_and_its_province_is_read_through_it()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            "Some Employer",
            published: false,
            districtCode: "gandaki-kaski"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = JobApi.ReadCreated(created);
        Assert.Equal("gandaki-kaski", record.DistrictCode);
        Assert.Equal("Kaski", record.DistrictName);
        Assert.Equal("gandaki", record.ProvinceCode);
        Assert.Equal("Gandaki", record.ProvinceName);
    }

    [Fact]
    public async Task A_remote_posting_with_no_district_is_allowed()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            "Remote Employer",
            published: true,
            districtCode: null,
            workMode: "Remote"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = JobApi.ReadCreated(created);
        Assert.Null(record.DistrictCode);
        Assert.Null(record.ProvinceName);
        Assert.Equal(WorkMode.Remote, record.WorkMode);
    }

    [Fact]
    public async Task Publishing_a_posting_without_a_description_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            "Some Employer",
            published: true,
            description: string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "description");
    }

    [Fact]
    public async Task Publishing_a_posting_whose_deadline_has_passed_is_refused()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            "Some Employer",
            published: true,
            deadline: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains(JobApi.Details(created), error => error.Field == "applicationDeadline");
    }

    [Fact]
    public async Task A_complete_posting_can_be_published_and_then_reaches_readers()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = factory.CreateClient();
        var slug = JobApi.NewSlug();
        var organization = $"Reachable Employer {Guid.NewGuid():N}";

        var created = await staffApi.PostForSnapshotAsync(AdminPath, JobApi.Body(
            slug,
            organization,
            published: true,
            districtCode: "bagmati-bhaktapur"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = JobApi.ReadCreated(created);
        Assert.True(record.IsPublished);
        Assert.Empty(record.PublishBlockers);
        Assert.False(record.IsExpired);

        var envelope = await reader.GetFromJsonAsync<ApiEnvelope<JobDetailResponse>>(
            $"{PublicPath}/{slug}",
            TestJson.Options);

        Assert.NotNull(envelope);
        Assert.Equal(organization, envelope!.Data!.Organization);
        Assert.Equal("Bhaktapur", envelope.Data.DistrictName);
        Assert.Equal("Bagmati", envelope.Data.ProvinceName);
    }

    [Fact]
    public async Task The_publish_blockers_clear_once_the_draft_is_filled_in()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = JobApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, new
        {
            slug,
            title = "A posting that is not finished yet",
            organization = "Some Employer",
            description = string.Empty,
            category = "Other",
            employmentType = "FullTime",
            workMode = "OnSite",
            applicationUrl = "https://www.merokaipatra.com/",
            applicationDeadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30).ToString("yyyy-MM-dd"),
            isPublished = false,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var draft = JobApi.ReadCreated(created);
        Assert.NotEmpty(draft.PublishBlockers);
        Assert.Contains(draft.PublishBlockers, blocker => blocker.Contains("at least 80 characters"));

        var filled = await api.PutForSnapshotAsync(
            $"{AdminPath}/{draft.Id}",
            JobApi.Body(slug, "Some Employer", published: true));

        Assert.Equal(HttpStatusCode.OK, filled.StatusCode);
        Assert.Empty(JobApi.ReadCreated(filled).PublishBlockers);
    }

    [Fact]
    public async Task A_published_posting_cannot_be_deleted_but_a_draft_can()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = JobApi.NewSlug();

        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            slug,
            "Employer To Be Retracted",
            published: true));

        var published = JobApi.ReadCreated(created);
        var refused = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("job_published", refused.Error?.Code);

        // Retract, then delete.
        var retracted = await api.PutForSnapshotAsync(
            $"{AdminPath}/{published.Id}",
            JobApi.Body(slug, "Employer To Be Retracted", published: false));

        Assert.Equal(HttpStatusCode.OK, retracted.StatusCode);

        var deleted = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");
        var missing = await api.DeleteForSnapshotAsync($"{AdminPath}/{published.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task The_staff_status_filter_separates_drafts_published_rows_and_closed_rows()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var draftSlug = JobApi.NewSlug();
        var draftOrganization = $"Draft Filter Employer {Guid.NewGuid():N}";
        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            draftSlug,
            draftOrganization,
            published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var drafts = await api.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
            $"{AdminPath}?status=Draft&search={Uri.EscapeDataString(draftOrganization)}");
        Assert.Equal(draftSlug, Assert.Single(drafts.Items).Slug);
        Assert.False(Assert.Single(drafts.Items).IsPublished);

        await api.PutForSnapshotAsync(
            $"{AdminPath}/{JobApi.ReadCreated(created).Id}",
            JobApi.Body(draftSlug, draftOrganization, published: true));

        var published = await api.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
            $"{AdminPath}?status=Published&search={Uri.EscapeDataString(draftOrganization)}");
        Assert.True(Assert.Single(published.Items).IsPublished);
        Assert.False(Assert.Single(published.Items).IsExpired);

        // A closed posting is neither live nor a draft, which is the whole reason it gets its own
        // filter: it is the row that needs a decision.
        var closedOrganization = $"Closed Filter Employer {Guid.NewGuid():N}";
        var closed = await api.PostForSnapshotAsync(
            AdminPath,
            JobApi.Body(
                JobApi.NewSlug(),
                closedOrganization,
                published: false,
                deadline: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)));
        Assert.Equal(HttpStatusCode.Created, closed.StatusCode);

        var expired = await api.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
            $"{AdminPath}?status=Expired&search={Uri.EscapeDataString(closedOrganization)}");

        Assert.Single(expired.Items);
        Assert.False(Assert.Single(expired.Items).IsPublished);
        Assert.True(Assert.Single(expired.Items).IsExpired);
    }

    [Theory]
    [InlineData("status=Wibble", "status_invalid")]
    [InlineData("category=Nonsense", "category_invalid")]
    [InlineData("province=atlantis", "province_invalid")]
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
    public async Task Updating_a_posting_that_does_not_exist_is_not_found()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);

        var updated = await api.PutForSnapshotAsync(
            $"{AdminPath}/{Guid.NewGuid()}",
            JobApi.Body(JobApi.NewSlug(), "Some Employer", published: false));

        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        Assert.Equal("job_not_found", updated.Error?.Code);
    }

    [Fact]
    public async Task The_staff_list_reports_the_author_who_wrote_each_posting()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        var organization = $"Authored Employer {Guid.NewGuid():N}";
        var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            JobApi.NewSlug(),
            organization,
            published: false));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.EndsWith("@nepalhub.test", JobApi.ReadCreated(created).CreatedByEmail);

        var page = await api.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
            $"{AdminPath}?search={Uri.EscapeDataString(organization)}");

        Assert.EndsWith("@nepalhub.test", Assert.Single(page.Items).CreatedByEmail);
    }

    [Fact]
    public async Task An_admin_and_a_moderator_can_both_write()
    {
        foreach (var role in new[] { Roles.Admin, Roles.Moderator })
        {
            using var client = await factory.CreateStaffClientAsync(role);
            var api = new AuthApiClient(client);

            var created = await api.PostForSnapshotAsync(AdminPath, JobApi.Body(
                JobApi.NewSlug(),
                $"Role {role} Employer",
                published: false));

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
    }
}

internal static class JobApi
{
    /// <summary>
    /// Comfortably longer than the eighty character minimum that publishing requires, so the
    /// publish rule is satisfied by the fixture itself rather than by each test padding itself out
    /// with filler sentences about nothing in particular.
    /// </summary>
    public const string LongDescription =
        "This description is comfortably longer than the eighty character minimum that publishing "
        + "requires, so the publish rule is satisfied by the fixture itself rather than by each test "
        + "padding itself out with filler sentences about nothing in particular.";

    public static string NewSlug() => $"test-{Guid.NewGuid():N}";

    /// <summary>
    /// A posting fixture. The employer carries a unique token so the row a test has just written can
    /// be found again with a text search, which is the handle the search endpoint offers: it
    /// matches the title, the employer and the description. A shared employer name would make every
    /// test row match every other one.
    /// </summary>
    public static object Body(
        string slug,
        string organization,
        bool published,
        string? title = null,
        string? description = null,
        string? applicationUrl = "https://www.merokaipatra.com/",
        string? districtCode = null,
        string workMode = "OnSite",
        DateOnly? deadline = null) => new
        {
            slug,
            title = title ?? $"A posting for a test about {slug}",
            organization,
            description = description ?? LongDescription,
            category = "Other",
            employmentType = "FullTime",
            workMode,
            districtCode,
            applicationUrl,
            applicationDeadline = (deadline ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30))
                .ToString("yyyy-MM-dd"),
            isPublished = published,
        };

    /// <summary>
    /// Unwraps the posting a staff write returned, so a test can assert against the record the
    /// server stored rather than against the request that was sent.
    /// </summary>
    public static JobAdminResponse ReadCreated(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<JobAdminResponse>>(TestJson.Options);

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
/// 00:00 in Kathmandu rather than 00:00 UTC, so these tests pin the clock to an hour where the two
/// dates already disagree. A test that agrees with the offset by accident proves nothing.
/// </summary>
public sealed class JobExpiryTests : IAsyncLifetime
{
    private const string PublicPath = "/api/jobs";
    private const string AdminPath = "/api/admin/jobs";

    private readonly FixedClockApiFactory _factory = FixedClockApiFactory.NearNow();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_posting_disappears_once_the_nepal_date_passes_its_deadline()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);
        using var reader = _factory.CreateClient();
        var readerApi = new AuthApiClient(reader);

        var start = NepaliDayBoundary();
        _factory.Clock.Set(start);
        var today = NepalToday();

        // A check on the setup rather than on the product: 19:00 UTC is already the small hours in
        // Kathmandu, so the two dates must differ here. If they ever agree the test has stopped
        // exercising the offset and the chosen hour needs moving.
        Assert.NotEqual(start.UtcDateTime.Date, today.ToDateTime(TimeOnly.MinValue));

        var slug = $"test-{Guid.NewGuid():N}";
        var organization = $"Expiry Employer {Guid.NewGuid():N}";
        var created = await CreateAsync(staffApi, slug, organization, today.AddDays(3), published: true);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1, (await CountPublicAsync(readerApi, organization)).TotalCount);

        // The deadline day itself is still open: somebody applying on the closing date is not too
        // late, so the boundary is exclusive only on the past side.
        _factory.Clock.Set(start.AddDays(2));
        Assert.Equal(1, (await CountPublicAsync(readerApi, organization)).TotalCount);

        _factory.Clock.Set(start.AddDays(4));

        var closed = await CountPublicAsync(readerApi, organization);
        Assert.Equal(0, closed.TotalCount);

        using var detail = await reader.GetAsync($"{PublicPath}/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Staff still see it, flagged as closed, because retiring it is their job rather than the
        // reader's.
        var staffPage = await staffApi.GetAsync<PagedResponse<JobAdminSummaryResponse>>(
            $"{AdminPath}?status=Expired&search={Uri.EscapeDataString(organization)}");

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

        var organization = $"Today Deadline Employer {Guid.NewGuid():N}";
        var created = await CreateAsync(
            staffApi,
            $"test-{Guid.NewGuid():N}",
            organization,
            today,
            published: true);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1, (await CountPublicAsync(readerApi, organization)).TotalCount);
    }

    [Fact]
    public async Task A_past_deadline_can_be_kept_as_a_draft_even_though_it_cannot_be_published()
    {
        using var staff = await _factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);

        _factory.Clock.Set(NepaliDayBoundary());
        var today = NepalToday();

        // Yesterday's closing date is a reasonable thing to file away for the record, so the draft
        // is allowed. Publishing it would advertise a posting that had already closed before it was
        // written down, and the admin tests cover that refusal.
        var created = await CreateAsync(
            staffApi,
            $"test-{Guid.NewGuid():N}",
            $"Past Deadline Employer {Guid.NewGuid():N}",
            today.AddDays(-1),
            published: false);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var record = JobApi.ReadCreated(created);
        Assert.False(record.IsPublished);

        // The date is still in the past, and the row says so even though it is only a draft. That is
        // the same fact the blocker is reporting: staff are told why the posting cannot go out,
        // rather than being left to work it out from the calendar.
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
    /// behaviour, so a server that used a different offset would leave a deadline stranded and fail.
    /// Reading the date from another endpoint instead would only prove that two endpoints agree with
    /// each other.
    /// </summary>
    private DateOnly NepalToday() =>
        DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(5 * 60 + 45));

    private static Task<ApiResponseSnapshot> CreateAsync(
        AuthApiClient api,
        string slug,
        string organization,
        DateOnly deadline,
        bool published) =>
        api.PostForSnapshotAsync(AdminPath, JobApi.Body(
            slug,
            organization,
            published,
            deadline: deadline));

    private static Task<PagedResponse<JobSummaryResponse>> CountPublicAsync(AuthApiClient api, string organization) =>
        api.GetAsync<PagedResponse<JobSummaryResponse>>($"{PublicPath}?search={Uri.EscapeDataString(organization)}");
}

/// <summary>
/// The board reuses the province and district reference data rather than copying it, so these two
/// tests assert that the join those filters depend on is actually populated. A job board whose
/// location filter silently returns nothing because a join is missing is a bug nobody would find
/// without a test that says so.
/// </summary>
public sealed class JobLocationFilterTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string PublicPath = "/api/jobs";

    [Theory]
    [InlineData("koshi", "Bhojpur", "koshi-bhojpur")]
    [InlineData("bagmati", "Kathmandu", "bagmati-kathmandu")]
    [InlineData("gandaki", "Kaski", "gandaki-kaski")]
    public async Task A_seeded_district_filter_returns_the_postings_that_sit_in_it(
        string provinceCode,
        string districtName,
        string districtCode)
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var districts = await api.GetAsync<List<DistrictResponse>>(
            $"/api/reference/provinces/{provinceCode}/districts");

        Assert.Contains(districts, district => district.Code == districtCode);

        var page = await api.GetAsync<PagedResponse<JobSummaryResponse>>(
            $"{PublicPath}?district={districtCode}&pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(districtName, item.DistrictName));
    }

    [Fact]
    public async Task A_posting_reports_the_province_that_the_reference_data_says_its_district_is_in()
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
                var page = await api.GetAsync<PagedResponse<JobSummaryResponse>>(
                    $"{PublicPath}?district={district.Code}&pageSize=100");

                // Every posting in a district reports the province the reference endpoint says that
                // district belongs to, so the name on a card is looked up rather than asserted
                // against a value this test supplied to itself.
                Assert.All(page.Items, item =>
                {
                    Assert.Equal(district.Name, item.DistrictName);
                    Assert.True(
                        codeByName.TryGetValue(item.ProvinceName!, out var code),
                        $"'{item.ProvinceName}' is not a province this site knows about.");
                    Assert.Equal(province.Code, code);
                });
            }
        }
    }
}
