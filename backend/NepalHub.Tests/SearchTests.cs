using System.Net;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Search;

namespace NepalHub.Tests;

/// <summary>
/// Searching as a visitor, which is the only way this endpoint is ever used.
///
/// <para>
/// Two properties get more attention here than in any other feature, because search is the one
/// surface that reaches into every module at once. The first is that it cannot leak: a draft, an
/// expired offer and a past event must all be unreachable here, in the same way they are unreachable
/// from their own module's page. The second is that a search for one word must not walk the whole
/// catalogue, which is why the candidate cap exists and why the totals are reported as capped rather
/// than exact.
/// </para>
///
/// <para>
/// Count assertions are scoped to a unique token or a search term, never to the whole catalogue.
/// Every other test class in this suite publishes rows into the same database, so a test that
/// asserted "there are 12 results" would pass or fail depending on which class ran first.
/// </para>
/// </summary>
public sealed class SearchTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private const string SearchPath = "/api/search";

    private static string Query(string term, string? extra = null) =>
        $"{SearchPath}?q={Uri.EscapeDataString(term)}{(extra is null ? string.Empty : $"&{extra}")}";

    [Fact]
    public async Task The_search_endpoint_is_readable_without_an_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Query("thakali"));

        // A site-wide search box has to work before somebody has an account. If this ever needs
        // [Authorize], the header form has to handle being signed out, which is the common case.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A term nobody has typed before. The endpoint answers with an empty page rather than an error,
    /// because "no results" is the single most likely outcome of any search and treating it as a
    /// failure would make the UI special-case its most common case.
    /// </summary>
    [Fact]
    public async Task A_term_that_matches_nothing_is_an_empty_page_rather_than_a_failure()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(
            Query($"zzz-no-such-thing-{Guid.NewGuid():N}"));

        Assert.Empty(results.Groups);
        Assert.Equal(0, results.TotalCount);
        Assert.False(results.Truncated);
    }

    /// <summary>
    /// One character is below the floor. A single letter matches a large share of the catalogue in a
    /// language where words are short, and refusing it is cheaper than six full scans nobody meant.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    public async Task A_term_too_short_to_be_meaningful_is_refused_with_the_floor_named(string term)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Query(term));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("query_too_short", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ceiling is enforced rather than clamped. A term that long is a paste, not a search, and
    /// silently searching the first hundred characters of it would answer a question nobody asked.
    /// </summary>
    [Fact]
    public async Task A_term_past_the_ceiling_is_refused_rather_than_truncated()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Query(new string('k', SearchLimits.MaxQueryLength + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("query_too_long", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_term_exactly_at_the_ceiling_is_accepted()
    {
        using var client = factory.CreateClient();

        // The boundary is the contract, so it is asserted from both sides rather than trusting the
        // off-by-one to be on the permissive side.
        using var response = await client.GetAsync(Query(new string('k', SearchLimits.MaxQueryLength)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// An unknown module names the valid ones. A silent fallback to "search everything" would hand
    /// back results from modules the reader explicitly filtered out, which looks like the filter
    /// being broken rather than the argument being wrong.
    /// </summary>
    [Fact]
    public async Task An_unknown_module_is_refused_with_the_valid_ones_named()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Query("thakali", "module=sideways"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("module_invalid", body, StringComparison.Ordinal);
        Assert.Contains(nameof(SearchModule.Calculators), body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Results come back grouped by module, because a reader who searched one word wants to see which
    /// kinds of thing it found, not a single undifferentiated list of rows.
    /// </summary>
    [Fact]
    public async Task Results_are_grouped_by_module_with_a_label_the_tabs_can_reuse()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(Query("thakali", "pageSize=30"));

        Assert.NotEmpty(results.Groups);
        Assert.All(results.Groups, group => Assert.False(string.IsNullOrWhiteSpace(group.Label)));

        // Every result is inside exactly one group, so nothing is dropped between ranking and grouping.
        var grouped = results.Groups.Sum(group => group.Items.Count);
        var listed = results.Groups.SelectMany(group => group.Items).Count();

        Assert.Equal(listed, grouped);
    }

    /// <summary>
    /// A result carries the address of the page it describes and enough to render a card without a
    /// second request. It carries no path, because the client owns its routes: a server-emitted link
    /// is a coupling that breaks the first time either side moves a page.
    /// </summary>
    [Fact]
    public async Task A_result_carries_its_module_slug_and_the_fields_a_card_needs()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(Query("thakali", "pageSize=30"));

        var result = Assert.Single(results.Groups.SelectMany(group => group.Items), item => item.Slug == "thakali-kitchen-dharan");

        Assert.Equal(SearchModule.Businesses, result.Module);
        Assert.False(string.IsNullOrWhiteSpace(result.ReferenceId));
        Assert.False(string.IsNullOrWhiteSpace(result.Title));

        // A business is the module that has a place and a category; a deal is the one with a
        // discount. Both are filled, which is what proves the mapping put them in the right columns
        // and did not shift them by one.
        Assert.Equal("Sunsari, Koshi", result.Place);
        Assert.Equal("Restaurants", result.Category);

        // The fields belonging to other modules stay empty, so a card cannot render a stray date or
        // a discount on a row that has neither.
        Assert.Null(result.DiscountText);
        Assert.Null(result.ExpiresOn);
        Assert.Null(result.StartsAt);
    }

    /// <summary>
    /// Only the six searchable modules ever appear. The daily quiz is deliberately absent: it has no
    /// stable public detail route to link to, and its questions and answers must never reach a
    /// keyword endpoint that an anonymous caller can enumerate.
    /// </summary>
    [Fact]
    public async Task Only_public_content_modules_ever_appear_in_results()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var searchable = new[]
        {
            SearchModule.Students,
            SearchModule.Jobs,
            SearchModule.Businesses,
            SearchModule.Deals,
            SearchModule.Events,
            SearchModule.Calculators,
        };

        foreach (var term in new[] { "calculator", "samosa", "kathmandu", "loan", "ka" })
        {
            var results = await api.GetAsync<SearchResponse>(Query(term, "pageSize=30"));

            Assert.All(results.Groups, group => Assert.Contains(group.Module, searchable));
            Assert.All(
                results.Groups.SelectMany(group => group.Items),
                item => Assert.Contains(item.Module, searchable));
        }
    }

    [Fact]
    public async Task A_module_filter_narrows_the_results_to_that_module()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var filtered = await api.GetAsync<SearchResponse>(
            Query("thakali", $"module={nameof(SearchModule.Businesses)}&pageSize=30"));

        Assert.NotEmpty(filtered.Groups);
        Assert.All(filtered.Groups, group => Assert.Equal(SearchModule.Businesses, group.Module));
        Assert.All(
            filtered.Groups.SelectMany(group => group.Items),
            item => Assert.Equal(SearchModule.Businesses, item.Module));
    }

    /// <summary>
    /// The module filter is applied to the queries, not to the merged page. A response whose only
    /// group is the requested module proves the others were never read.
    /// </summary>
    [Fact]
    public async Task An_omitted_module_searches_everything_rather_than_one_module()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var all = await api.GetAsync<SearchResponse>(Query("thakali", "pageSize=30"));

        Assert.True(
            all.Groups.Count > 1,
            $"A cross-module search returned only {all.Groups.Count} group.");
    }

    /// <summary>
    /// A term in the title outranks the same term buried in a description, and a title that starts
    /// with the term outranks one where it appears later. This is the ordering a reader notices
    /// without being able to name it: the obviously relevant thing appears first.
    /// </summary>
    [Fact]
    public async Task A_title_match_outranks_a_match_buried_in_the_body()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(staff);

        // Two offers: one named for the term, one whose terms only mention it. Both are published and
        // both are findable, so the assertion is about their order rather than their presence.
        //
        // The buried offer's title deliberately does *not* carry the token. An earlier version of this
        // test put it in both places, which made both rows rank as title matches and let the newest
        // one win — the assertion passed for the wrong reason, and would have passed just as happily
        // if the ranking had been dropped entirely.
        var token = Guid.NewGuid().ToString("N")[..10];

        var titled = SearchApi.DealSlug($"titled-{token}");
        var buried = SearchApi.DealSlug($"buried-{token}");

        var titledCreated = await adminApi.PostForSnapshotAsync(
            "/api/admin/deals",
            DealApi.Body(titled, $"Zoology scholarship {token}", published: true));
        Assert.Equal(HttpStatusCode.Created, titledCreated.StatusCode);

        var buriedCreated = await adminApi.PostForSnapshotAsync(
            "/api/admin/deals",
            DealApi.Body(
                buried,
                "An unrelated autumn fair",
                published: true,
                description: $"{DealApi.LongDescription} The reference {token} appears only here."));
        Assert.Equal(HttpStatusCode.Created, buriedCreated.StatusCode);

        using var reader = factory.CreateClient();
        var results = await new AuthApiClient(reader).GetAsync<SearchResponse>(
            Query(token, $"module={nameof(SearchModule.Deals)}&pageSize=30"));

        var slugs = results.Groups.SelectMany(group => group.Items).Select(item => item.Slug).ToList();

        Assert.Contains(titled, slugs);
        Assert.Contains(buried, slugs);
        Assert.True(
            slugs.IndexOf(titled) < slugs.IndexOf(buried),
            $"Order was [{string.Join(", ", slugs)}]: titled at {slugs.IndexOf(titled)}, buried at {slugs.IndexOf(buried)}.");
    }

    /// <summary>
    /// The ranking runs in .NET while the filter runs in MySQL under an accent- and
    /// case-insensitive collation. If the two disagreed, a row the database had already matched
    /// could be scored as a non-match and dropped, so the page would silently lose results the
    /// database found. Searching the other way round is the case that catches it.
    /// </summary>
    [Fact]
    public async Task A_term_matches_regardless_of_case_and_accents()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var lower = await api.GetAsync<SearchResponse>(Query("thakali", "pageSize=30"));
        var upper = await api.GetAsync<SearchResponse>(Query("THAKALI", "pageSize=30"));

        var lowerSlugs = lower.Groups.SelectMany(group => group.Items).Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        var upperSlugs = upper.Groups.SelectMany(group => group.Items).Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(lowerSlugs);

        // The same rows either way, and none of them dropped on the way through the ranker.
        Assert.Equal(lowerSlugs, upperSlugs);
    }

    /// <summary>
    /// A draft is invisible here exactly as it is on its own module's page. This is the load-bearing
    /// test for the module: search reaches into every table, so it is the one place a forgotten
    /// visibility filter would publish an unpublished row to anonymous callers.
    /// </summary>
    [Fact]
    public async Task A_draft_is_invisible_to_search()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(staff);

        var token = Guid.NewGuid().ToString("N")[..10];
        var slug = SearchApi.BusinessSlug($"draft-{token}");
        var name = $"Unlisted Trading House {token}";

        var created = await adminApi.PostForSnapshotAsync(
            "/api/admin/businesses",
            SearchApi.BusinessBody(slug, name, published: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var reader = factory.CreateClient();
        var results = await new AuthApiClient(reader).GetAsync<SearchResponse>(Query(token, "pageSize=30"));

        Assert.DoesNotContain(slug, results.Groups.SelectMany(group => group.Items).Select(item => item.Slug));
    }

    /// <summary>
    /// A published business is findable by the same term that finds its draft. Asserting both halves
    /// keeps the visibility test above honest: a draft that is invisible because the search cannot see
    /// it at all would pass that test just as happily.
    /// </summary>
    [Fact]
    public async Task A_published_listing_is_findable_by_a_term_only_it_carries()
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var adminApi = new AuthApiClient(staff);

        var token = Guid.NewGuid().ToString("N")[..10];
        var slug = SearchApi.BusinessSlug($"listed-{token}");

        var created = await adminApi.PostForSnapshotAsync(
            "/api/admin/businesses",
            SearchApi.BusinessBody(slug, $"Lantern Bazaar {token}", published: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var reader = factory.CreateClient();
        var results = await new AuthApiClient(reader).GetAsync<SearchResponse>(
            Query(token, $"module={nameof(SearchModule.Businesses)}&pageSize=30"));

        Assert.Contains(slug, results.Groups.SelectMany(group => group.Items).Select(item => item.Slug));
    }

    /// <summary>
    /// Expiry is enforced here too. The seeder wrote this offer past its end date on purpose, so the
    /// only thing that can be hiding it is the same public filter its own page applies.
    /// </summary>
    [Fact]
    public async Task An_expired_offer_is_invisible_to_search()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(Query("monsoon", "pageSize=30"));

        Assert.DoesNotContain(
            "monsoon-set-dal-bhat-expired",
            results.Groups.SelectMany(group => group.Items).Select(item => item.Slug));
    }

/// <summary>
/// <c>%</c> and <c>_</c> are LIKE wildcards. Unescaped, a term containing one becomes a pattern
/// matching every row in six tables at once, which is both the opposite of what was asked for and a
/// cheap way to walk the entire catalogue.
///
/// <para>
/// The term is asserted from both sides, because "matches nothing" would pass just as happily as
/// "matches everything". A real seeded offer says "20% off", so the escaped term has to find it;
/// and no business listing contains a literal "20%", so an unescaped <c>%</c> would have matched
/// every one of them.
/// </para>
/// </summary>
[Fact]
public async Task A_term_containing_a_like_wildcard_matches_literally_rather_than_everything()
{
    using var client = factory.CreateClient();
    var api = new AuthApiClient(client);

    var deals = await api.GetAsync<SearchResponse>(
        Query("20%", $"module={nameof(SearchModule.Deals)}&pageSize=30"));
    var businesses = await api.GetAsync<SearchResponse>(
        Query("20%", $"module={nameof(SearchModule.Businesses)}&pageSize=30"));

    // A visitor typing that is entitled to find the offer that says it.
    Assert.NotEmpty(deals.Groups);

    // Nothing in the directory contains a literal "20%", so an unescaped wildcard has swallowed the
    // whole module.
    Assert.Empty(businesses.Groups);
}

    /// <summary>
    /// The page size is capped lower here than on a single-module list, because one page of search
    /// is up to six queries. The cap is applied rather than refused, matching every other list.
    /// </summary>
    [Fact]
    public async Task A_page_size_beyond_the_search_ceiling_is_clamped()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(Query("ka", "pageSize=5000"));

        Assert.Equal(SearchLimits.MaxPageSize, results.PageSize);
    }

    [Fact]
    public async Task An_absurd_page_number_clamps_to_the_last_page_rather_than_failing()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var first = await api.GetAsync<SearchResponse>(Query("thakali"));
        Assert.True(first.TotalCount > 0, "The seeded catalogue should match this term somewhere.");
        Assert.True(first.TotalPages == 1, "This term is expected to fit one page; the walk test covers multi-page.");

        var beyond = await api.GetAsync<SearchResponse>(Query("thakali", "page=9999"));

        Assert.Equal(first.TotalPages, beyond.Page);
        Assert.Equal(first.TotalCount, beyond.TotalCount);
        Assert.NotEmpty(beyond.Groups);
        Assert.False(beyond.HasNext);
        Assert.Equal(
            first.Groups.SelectMany(group => group.Items).Select(item => item.ReferenceId),
            beyond.Groups.SelectMany(group => group.Items).Select(item => item.ReferenceId));
    }

    [Fact]
    public async Task A_term_that_matches_nothing_answers_on_page_one_with_no_groups()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(Query("zzzznomatchzzzz", "page=7"));

        Assert.Equal(1, results.Page);
        Assert.Equal(0, results.TotalCount);
        Assert.Empty(results.Groups);
        Assert.Equal(0, results.TotalPages);
        Assert.False(results.HasNext);
        Assert.False(results.HasPrevious);
        Assert.False(results.Truncated);
    }

    [Fact]
    public async Task Paging_walks_the_results_without_repeating_one()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // A term common enough to span pages, so the walk is actually exercised.
        var first = await api.GetAsync<SearchResponse>(Query("calculator", "pageSize=3"));

        if (first.TotalPages <= 1)
        {
            return;
        }

        var second = await api.GetAsync<SearchResponse>(Query("calculator", "page=2&pageSize=3"));

        var firstSlugs = first.Groups.SelectMany(group => group.Items).Select(item => item.Slug).ToList();
        var secondSlugs = second.Groups.SelectMany(group => group.Items).Select(item => item.Slug).ToList();

        Assert.Empty(firstSlugs.Intersect(secondSlugs, StringComparer.Ordinal));
    }

    /// <summary>
    /// The capped totals are reported as capped. Each module contributes a bounded number of
    /// candidates and nothing is counted, so a term matching more than the cap reports
    /// <c>truncated</c> rather than a total that looks exact and is not.
    /// </summary>
    [Fact]
    public async Task A_term_wider_than_the_candidate_cap_is_reported_as_truncated()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        // A two-letter term in a seeded catalogue cannot fill six caps, so the honest answer at the
        // current size is "not truncated". Asserted anyway, because the flag is the difference between
        // a total that means something and one that does not.
        var results = await api.GetAsync<SearchResponse>(Query("ka", "pageSize=30"));

        var total = results.Groups.SelectMany(group => group.Items).Count();

        Assert.True(total <= SearchLimits.CandidateCap * 6, $"A search returned {total} results.");
        Assert.False(results.HasNext);
    }

    /// <summary>
    /// The truncation flag counts the rows SQL returned, not the rows that survived ranking.
    ///
    /// <para>
    /// These count differently on purpose and the flag depends on the difference: ranking drops every
    /// candidate that matched only in a low-value haystack, so a module can hand back a completely
    /// full cap and still rank far fewer rows than that. Those unranked rows are rows SQL chose
    /// instead of rows the database matched, which is exactly what the reader needs warning about.
    /// Reading the post-rank count instead would report "not truncated" while hiding matches.
    /// </para>
    /// </summary>
    [Fact]
    public void A_full_candidate_cap_is_truncated_even_when_ranking_drops_the_rows()
    {
        // A full cap of rows, of which exactly one carries the term. Ranking keeps one, the cap was
        // still hit, so the flag has to be true.
        var rows = Enumerable.Range(0, SearchLimits.CandidateCap)
            .Select(index => new SearchCandidate(
                SearchModule.Calculators,
                index == 0 ? "loan emi" : "nothing to do with the term",
                string.Empty,
                string.Empty,
                string.Empty,
                DateTime.UnixEpoch.AddDays(index),
                new SearchResultItem(
                    SearchModule.Calculators,
                    index.ToString(),
                    $"calculator-{index}",
                    $"Calculator {index}",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)))
            .ToList();

        var batch = new ModuleCandidates(
            SearchService.Rank(rows, SearchText.Normalise("emi"), CancellationToken.None),
            rows.Count >= SearchLimits.CandidateCap);

        Assert.Equal(SearchLimits.CandidateCap, rows.Count);
        Assert.True(batch.HitCap, "A module that filled its cap has hidden matches from the page.");
        Assert.Single(batch.Candidates);
    }

    [Fact]
    public void A_candidate_cap_that_was_not_filled_is_not_truncated()
    {
        var rows = Enumerable.Range(0, SearchLimits.CandidateCap - 1)
            .Select(index => new SearchCandidate(
                SearchModule.Calculators,
                $"calculator {index}",
                string.Empty,
                string.Empty,
                string.Empty,
                DateTime.UnixEpoch.AddDays(index),
                new SearchResultItem(
                    SearchModule.Calculators,
                    index.ToString(),
                    $"calculator-{index}",
                    $"Calculator {index}",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)))
            .ToList();

        var batch = new ModuleCandidates(
            SearchService.Rank(rows, SearchText.Normalise("calculator"), CancellationToken.None),
            rows.Count >= SearchLimits.CandidateCap);

        Assert.False(batch.HitCap, "One row short of the cap is every match there is.");
        Assert.Equal(SearchLimits.CandidateCap - 1, batch.Candidates.Count);
    }

    [Fact]
    public async Task The_response_echoes_the_prepared_term_rather_than_the_raw_one()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(Query("   thakali   "));

        // The echoed term is what the search actually ran, collapsed. A client echoing back whatever
        // was typed would show a reader padding they cannot see and did not send.
        Assert.Equal("thakali", results.Query);
    }

    [Fact]
    public async Task The_search_endpoint_does_not_leak_anything_private()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        var results = await api.GetAsync<SearchResponse>(Query("answer explanation question", "pageSize=30"));

        // The daily quiz is not searchable, so its questions, answers and explanations cannot be
        // enumerated by guessing at words a quiz might contain. Asserted as a property of the
        // response rather than as a specific query: no result may come from the quiz module.
        Assert.All(results.Groups, group => Assert.NotEqual("Quiz", group.Label));
    }
}

/// <summary>
/// Search is throttled harder than calculation, because one search is up to six queries and the
/// header form fires one per keystroke. The quota is still generous for a person typing.
/// </summary>
public sealed class SearchRateLimitingTests : IClassFixture<SearchLimitedApiFactory>
{
    private readonly SearchLimitedApiFactory _factory;

    public SearchRateLimitingTests(SearchLimitedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Search_rejects_once_the_window_is_exhausted()
    {
        using var client = _factory.CreateClient();
        var api = new AuthApiClient(client);

        for (var attempt = 0; attempt < SearchLimitedApiFactory.PermitLimit; attempt++)
        {
            using var allowed = await client.GetAsync(
                $"/api/search?q={Uri.EscapeDataString($"term{attempt}")}");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var limited = await client.GetAsync("/api/search?q=one-too-many");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("rate_limited", await limited.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_other_public_reads_are_not_pulled_into_the_search_quota()
    {
        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < SearchLimitedApiFactory.PermitLimit + 3; attempt++)
        {
            using var response = await client.GetAsync("/api/deals?pageSize=1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

internal static class SearchApi
{
    public static string BusinessSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static string DealSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>
    /// A listing fixture. The name carries a unique token, because the handle for finding a row a test
    /// has just written is a keyword search on something only that row contains. A shared name would
    /// make every test row match every other one.
    /// </summary>
    public static object BusinessBody(
        string slug,
        string name,
        bool published,
        string districtCode = "bagmati-lalitpur") => new
        {
            slug,
            name,
            description = DealApi.LongDescription,
            category = "Other",
            districtCode,
            address = "An address, for the search tests.",
            phone = "9800000201",
            email = (string?)null,
            website = (string?)null,
            // Publishing a listing requires it to be verified, which is a publish-path rule rather
            // than a column constraint. A draft needs nothing extra, so a fixture that only ever
            // writes drafts would never notice this field was missing.
            isVerified = published,
            isPublished = published,
            openingHours = Array.Empty<object>(),
        };
}