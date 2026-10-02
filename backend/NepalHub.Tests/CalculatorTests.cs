using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Calculators;

namespace NepalHub.Tests;

public sealed class CalculatorCatalogueTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private static readonly string[] SeededSlugs = ["loan-emi", "compound-interest", "percentage-calculator"];

    [Fact]
    public async Task The_shipped_calculators_are_seeded_in_display_order()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync(client, "/api/calculators?pageSize=100");

        Assert.All(SeededSlugs, slug => Assert.Contains(page.Items, item => item.Slug == slug));
        Assert.Equal(SeededSlugs, page.Items.Select(item => item.Slug));
        Assert.All(page.Items, item => Assert.True(item.IsPublished));
    }

    [Fact]
    public async Task The_catalogue_is_paged_and_reports_a_total()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync(client, "/api/calculators?page=1&pageSize=2");

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.False(page.HasPrevious);
        Assert.True(page.HasNext);

        var second = await ReadPageAsync(client, "/api/calculators?page=2&pageSize=2");
        Assert.Single(second.Items);
        Assert.True(second.HasPrevious);
        Assert.False(second.HasNext);
        Assert.Empty(page.Items.Select(item => item.Slug).Intersect(second.Items.Select(item => item.Slug)));
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(-5, 10, 10)]
    [InlineData(1, 500, 100)]
    [InlineData(1, 0, 24)]
    [InlineData(2, 10, 10)]
    [InlineData(9999, 10, 10)]
    public async Task An_out_of_range_page_request_is_clamped_rather_than_rejected(
        int requestedPage,
        int requestedPageSize,
        int expectedPageSize)
    {
        using var client = factory.CreateClient();

        // The catalogue is small enough that a single page holds it, but the last page is still read
        // back from the endpoint so the case stays honest if the catalogue grows.
        var baseline = await ReadPageAsync(client, $"/api/calculators?page=1&pageSize={expectedPageSize}");
        var lastPage = PageRequest.TotalPagesFor(baseline.TotalCount, expectedPageSize);
        var expectedPage = Math.Clamp(requestedPage < 1 ? 1 : requestedPage, 1, lastPage);

        var result = await ReadPageAsync(client, $"/api/calculators?page={requestedPage}&pageSize={requestedPageSize}");

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
        Assert.True(result.Page <= lastPage, "The clamped page must never be past the last page with rows.");
    }

    [Fact]
    public async Task A_page_past_the_end_returns_the_last_page_rather_than_an_empty_one()
    {
        using var client = factory.CreateClient();

        var first = await ReadPageAsync(client, "/api/calculators?page=1&pageSize=1");
        Assert.True(first.TotalCount > 0, "The catalogue needs at least one published calculator for this to mean anything.");

        var last = await ReadPageAsync(client, $"/api/calculators?page={first.TotalCount}&pageSize=1");

        Assert.Equal(first.TotalCount, last.Page);
        Assert.Single(last.Items);
        Assert.Equal(first.TotalCount, last.TotalPages);
        Assert.False(last.HasNext);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_answers_on_page_one_with_no_items()
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync(client, "/api/calculators?search=zzzznomatchzzzz&page=7&pageSize=10");

        Assert.Equal(1, page.Page);
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNext);
    }

    [Fact]
    public async Task Search_matches_the_name_and_summary()
    {
        using var client = factory.CreateClient();

        var byName = await ReadPageAsync(client, "/api/calculators?search=compound");
        Assert.Equal("compound-interest", Assert.Single(byName.Items).Slug);

        var bySummary = await ReadPageAsync(client, "/api/calculators?search=instalment");
        Assert.Equal("loan-emi", Assert.Single(bySummary.Items).Slug);
    }

    [Fact]
    public async Task A_search_term_containing_a_wildcard_is_escaped()
    {
        using var client = factory.CreateClient();

        // An unescaped "%" would match every row.
        var page = await ReadPageAsync(client, "/api/calculators?search=%25");

        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData("Finance", "compound-interest", "loan-emi")]
    [InlineData("Everyday", "percentage-calculator")]
    [InlineData("Education")]
    [InlineData("Health")]
    [InlineData("Engineering")]
    public async Task The_catalogue_can_be_filtered_by_category(string category, params string[] expected)
    {
        using var client = factory.CreateClient();

        var page = await ReadPageAsync(client, $"/api/calculators?category={category}");

        Assert.Equal(expected.Length, page.TotalCount);
        Assert.Equal(expected, page.Items.Select(i => i.Slug).OrderBy(s => s));
    }

    [Fact]
    public async Task An_unknown_category_is_rejected_with_its_own_code()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/calculators?category=Astrology");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("category_invalid", envelope!.Error!.Code);
    }

    [Fact]
    public async Task A_detail_response_describes_the_whole_input_form()
    {
        using var client = factory.CreateClient();

        var calculator = await ReadAsync<CalculatorDetailResponse>(client, "/api/calculators/loan-emi");

        Assert.Equal("loan-emi", calculator.Slug);
        Assert.Equal(CalculatorCategory.Finance, calculator.Category);
        Assert.True(calculator.IsPublished);
        Assert.NotEmpty(calculator.Description);
        Assert.Equal(["principal", "annualRate", "years"], calculator.Inputs.Select(field => field.Key));
        Assert.All(calculator.Inputs, field =>
        {
            Assert.NotEmpty(field.Label);
            Assert.NotNull(field.Minimum);
            Assert.NotNull(field.Maximum);
            Assert.NotNull(field.DefaultValue);
        });
    }

    [Fact]
    public async Task An_unknown_slug_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/calculators/nowhere");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("calculator_not_found", envelope!.Error!.Code);
    }

    [Fact]
    public async Task Reading_the_catalogue_needs_no_account()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/calculators");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<PagedResponse<CalculatorSummaryResponse>> ReadPageAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PagedResponse<CalculatorSummaryResponse>>>(TestJson.Options);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }
}

/// <summary>Mirrors <c>PagedResult&lt;T&gt;</c> from the API.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class CalculatorCalculationTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    // Reference figures for NPR 2,000,000 at 10.5% nominal over 20 years, and for a
    // 0% loan where the instalment is simply the principal divided by 12.
    private const decimal ExpectedEmi = 19967.60m;
    private const decimal ExpectedTotal = 4792223.46m;

    [Fact]
    public async Task The_loan_emi_calculation_matches_the_published_formula()
    {
        using var client = factory.CreateClient();

        var result = await CalculatorApi.CalculateAsync(
            client,
            "loan-emi",
            new { principal = 2000000m, annualRate = 10.5m, years = 20m });

        var payment = Summary(result, "monthlyPayment");
        var total = Summary(result, "totalPayable");
        var interest = Summary(result, "totalInterest");

        Assert.InRange(payment, ExpectedEmi - 0.05m, ExpectedEmi + 0.05m);
        Assert.InRange(total, ExpectedTotal - 1.00m, ExpectedTotal + 1.00m);
        Assert.InRange(interest, ExpectedTotal - 2000000m - 1.00m, ExpectedTotal - 2000000m + 1.00m);
    }

    [Fact]
    public async Task A_zero_rate_loan_splits_the_principal_evenly()
    {
        using var client = factory.CreateClient();

        var result = await CalculatorApi.CalculateAsync(
            client,
            "loan-emi",
            new { principal = 120000m, annualRate = 0m, years = 1m });

        Assert.Equal(10000m, Summary(result, "monthlyPayment"));
        Assert.Equal(0m, Summary(result, "totalInterest"));
        Assert.Equal(120000m, Summary(result, "totalPayable"));
    }

    [Fact]
    public async Task A_calculation_returns_a_breakdown_and_notes()
    {
        using var client = factory.CreateClient();

        var result = await CalculatorApi.CalculateAsync(
            client,
            "compound-interest",
            new { principal = 100000m, annualRate = 8m, years = 10m, compoundsPerYear = 4m });

        Assert.NotEmpty(result.Result.Summary);
        Assert.NotEmpty(result.Result.Breakdown);
        Assert.NotEmpty(result.Result.Notes);
        Assert.Contains("compound-interest", result.Slug);
    }

    [Fact]
    public void A_categorical_line_serialises_as_text_with_no_number()
    {
        var line = new CalculationLine("band", "Category", ValueFormat.Text, Text: "Normal weight");

        var json = JsonSerializer.Serialize(line, TestJson.Options);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Text", document.RootElement.GetProperty("format").GetString());
        Assert.Equal("Normal weight", document.RootElement.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("value").ValueKind);
        Assert.Null(line.Value);
    }

    [Fact]
    public async Task A_value_sent_as_a_string_is_accepted()
    {
        using var client = factory.CreateClient();

        // The form posts text inputs, so numbers arrive as strings as often as not.
        var result = await CalculatorApi.CalculateAsync(
            client,
            "percentage-calculator",
            new { value = "2500", percent = "15" });

        Assert.Equal(375m, Summary(result, "result"));
    }

    [Fact]
    public async Task A_missing_required_input_is_reported_against_its_own_field()
    {
        using var client = factory.CreateClient();

        var snapshot = await CalculatorApi.SnapshotAsync(client, "loan-emi", new { principal = 1000000m });

        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);
        Assert.Equal("validation_failed", snapshot.Error!.Code);

        var errors = CalculatorApi.Details(snapshot);
        Assert.Contains(errors, error => error.Field == "annualRate" && error.Code == "required");
        Assert.Contains(errors, error => error.Field == "years" && error.Code == "required");
        Assert.DoesNotContain(errors, error => error.Field == "principal");
    }

    [Fact]
    public async Task A_non_numeric_input_is_rejected()
    {
        using var client = factory.CreateClient();

        var snapshot = await CalculatorApi.SnapshotAsync(
            client,
            "loan-emi",
            new { principal = "not a number", annualRate = 10m, years = 10m });

        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);

        var error = Assert.Single(CalculatorApi.Details(snapshot), item => item.Field == "principal");
        Assert.Equal("invalid", error.Code);
    }

    [Fact]
    public async Task An_input_outside_its_range_is_rejected_with_the_bound()
    {
        using var client = factory.CreateClient();

        var tooLow = await CalculatorApi.SnapshotAsync(client, "loan-emi", new { principal = 1m, annualRate = 10m, years = 10m });
        var tooHigh = await CalculatorApi.SnapshotAsync(
            client, "loan-emi", new { principal = 500_000_000m, annualRate = 10m, years = 10m });

        Assert.Equal("out_of_range", Assert.Single(CalculatorApi.Details(tooLow), item => item.Field == "principal").Code);
        Assert.Equal("out_of_range", Assert.Single(CalculatorApi.Details(tooHigh), item => item.Field == "principal").Code);
    }

    [Fact]
    public async Task An_unknown_input_key_is_rejected_instead_of_being_ignored()
    {
        using var client = factory.CreateClient();

        var snapshot = await CalculatorApi.SnapshotAsync(
            client,
            "percentage-calculator",
            new { value = 100m, percent = 10m, procent = 10m });

        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);

        // Silently dropping the typo would fall back to a default and answer with a
        // plausible but wrong figure.
        var error = Assert.Single(CalculatorApi.Details(snapshot), item => item.Field == "procent");
        Assert.Equal("unexpected", error.Code);
    }

    [Fact]
    public async Task Calculating_an_unknown_calculator_returns_not_found()
    {
        using var client = factory.CreateClient();

        var snapshot = await CalculatorApi.SnapshotAsync(client, "nowhere", new { value = 1m });

        Assert.Equal(HttpStatusCode.NotFound, snapshot.StatusCode);
        Assert.Equal("calculator_not_found", snapshot.Error!.Code);
    }

    [Fact]
    public async Task Calculating_needs_no_account()
    {
        using var client = factory.CreateClient();

        using var response = await CalculatorApi.PostAsync(
            client,
            "percentage-calculator",
            new { value = 100m, percent = 10m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static decimal Summary(CalculationResponse response, string key) =>
        response.Result.Summary.Single(line => line.Key == key).Value
        ?? throw new InvalidOperationException($"'{key}' was not a numeric line.");
}

/// <summary>Shared plumbing for the three calculator test classes.</summary>
internal static class CalculatorApi
{
    public static async Task<CalculationResponse> CalculateAsync(HttpClient client, string slug, object inputs)
    {
        using var response = await PostAsync(client, slug, inputs);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CalculationResponse>>(TestJson.Options);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }

    public static async Task<ApiResponseSnapshot> SnapshotAsync(HttpClient client, string slug, object inputs)
    {
        using var response = await PostAsync(client, slug, inputs);
        var json = await response.Content.ReadAsStringAsync();

        return new ApiResponseSnapshot(response.StatusCode, json);
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string slug, object inputs) =>
        await PostAsync(client, slug, inputs, await ReadCsrfTokenAsync(client));

    /// <summary>
    /// The antiforgery middleware guards every unsafe verb, including this anonymous
    /// one, so a visitor still has to present a token exactly as the browser does.
    /// </summary>
    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string slug,
        object inputs,
        string csrfToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/calculators/{slug}/calculate")
        {
            Content = JsonContent.Create(new { inputs }, options: TestJson.Options),
        };
        request.Headers.Add(AuthConstants.CsrfHeaderName, csrfToken);

        return await client.SendAsync(request);
    }

    public static async Task<string> ReadCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CsrfTokenResponse>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!.Token;
    }

    public static IReadOnlyList<FieldError> Details(ApiResponseSnapshot snapshot)
    {
        var error = snapshot.Error;
        Assert.NotNull(error);
        Assert.NotNull(error!.Details);

        return error.Details!.Value.Deserialize<FieldError[]>(TestJson.Options) ?? [];
    }
}

public sealed class CalculatorRateLimitTests(CalculateLimitedApiFactory factory)
    : IClassFixture<CalculateLimitedApiFactory>
{
    [Fact]
    public async Task Too_many_calculations_from_one_address_are_rejected()
    {
        using var client = factory.CreateClient();
        var body = new { inputs = new { value = 100m, percent = 10m } };
        var csrfToken = await CalculatorApi.ReadCsrfTokenAsync(client);

        for (var attempt = 0; attempt < CalculateLimitedApiFactory.PermitLimit; attempt++)
        {
            using var allowed = await CalculatorApi.PostAsync(
                client,
                "percentage-calculator",
                body.inputs,
                csrfToken);

            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await CalculatorApi.PostAsync(
            client,
            "percentage-calculator",
            body.inputs,
            csrfToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        var envelope = await rejected.Content.ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options);
        Assert.Equal("rate_limited", envelope!.Error!.Code);
    }
}

public sealed class CalculatorAdminTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task The_staff_list_is_closed_to_anonymous_visitors()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/admin/calculators");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_staff_list_is_closed_to_a_standard_account()
    {
        using var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail());

        using var response = await client.GetAsync("/api/admin/calculators");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_standard_user_cannot_reach_the_staff_calculator_endpoints()
    {
        using var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail());
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        using var listed = await client.GetAsync("/api/admin/calculators");
        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);

        var created = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody(slug));
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    [Fact]
    public async Task A_moderator_can_create_a_calculator_as_a_draft()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Moderator);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody(slug));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // A draft must not be visible to the public, and must be visible to staff.
        var publicPage = await ReadPageAsync<CalculatorSummaryResponse>(client, $"/api/calculators?search={slug}");
        Assert.Equal(0, publicPage.TotalCount);

        var staffPage = await ReadPageAsync<CalculatorAdminSummaryResponse>(client, $"/api/admin/calculators?search={slug}");
        Assert.Equal(slug, Assert.Single(staffPage.Items).Slug);
        Assert.False(staffPage.Items[0].IsPublished);
    }

    [Fact]
    public async Task Creating_a_calculator_whose_slug_is_taken_is_a_conflict()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var first = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody(slug));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody(slug));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("slug_taken", duplicate.Error!.Code);
    }

    [Fact]
    public async Task A_slug_cannot_be_changed_after_the_calculator_exists()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody(slug));
        var id = ReadId(created.Json);

        // Sending a different slug is a different row, so it is refused outright.
        var renamed = await api.PutForSnapshotAsync(
            $"/api/admin/calculators/{id}",
            DraftBody(NewSlug(), name: "A different name"));

        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
        Assert.Equal("slug_immutable", renamed.Error!.Code);
    }

    [Fact]
    public async Task Staff_can_update_the_copy_of_a_calculator()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody(slug));
        var id = ReadId(created.Json);

        var updated = await api.PutForSnapshotAsync(
            $"/api/admin/calculators/{id}",
            DraftBody(slug, name: "Renamed draft", summary: "An updated summary for the draft."));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var fetched = await api.GetAsync<CalculatorAdminResponse>($"/api/admin/calculators/{id}");
        Assert.Equal("Renamed draft", fetched.Name);
    }

    [Fact]
    public async Task A_draft_can_be_deleted_but_a_published_calculator_cannot()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);
        var slug = NewSlug();

        var created = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody(slug));
        var draftId = ReadId(created.Json);

        var deleted = await api.DeleteForSnapshotAsync($"/api/admin/calculators/{draftId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await api.DeleteForSnapshotAsync($"/api/admin/calculators/{draftId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        // A shipped calculator has been public, so it can only be retracted.
        var published = await ReadPageAsync<CalculatorAdminSummaryResponse>(
            client, "/api/admin/calculators?search=loan-emi");
        var loanEmi = Assert.Single(published.Items);

        var refused = await api.DeleteForSnapshotAsync($"/api/admin/calculators/{loanEmi.Id}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("calculator_published", refused.Error!.Code);
    }

    [Fact]
    public async Task A_calculator_with_no_implementation_cannot_be_published()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        var snapshot = await api.PostForSnapshotAsync(
            "/api/admin/calculators",
            DraftBody(NewSlug(), isPublished: true));

        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);
        Assert.Equal("validation_failed", snapshot.Error!.Code);

        var details = CalculatorApi.Details(snapshot);
        Assert.Equal("unsupported", Assert.Single(details, item => item.Field == "isPublished").Code);
    }

    [Fact]
    public async Task A_calculator_without_any_input_field_is_rejected()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        var snapshot = await api.PostForSnapshotAsync(
            "/api/admin/calculators",
            new
            {
                slug = NewSlug(),
                name = "No inputs",
                summary = "A calculator with no inputs at all.",
                description = "This record should never be accepted by the API.",
                category = "Everyday",
                sortOrder = 500,
                isPublished = false,
                inputs = Array.Empty<object>(),
            });

        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);
        Assert.Equal("inputs", Assert.Single(CalculatorApi.Details(snapshot)).Field);
    }

    [Fact]
    public async Task A_calculator_with_a_duplicate_input_key_is_rejected()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        var snapshot = await api.PostForSnapshotAsync(
            "/api/admin/calculators",
            new
            {
                slug = NewSlug(),
                name = "Clashing keys",
                summary = "Two fields share the same key.",
                description = "This record should be rejected because the keys clash.",
                category = "Everyday",
                sortOrder = 500,
                isPublished = false,
                inputs = new object[]
                {
                    new { key = "value", label = "Value", kind = "Number", required = true },
                    new { key = "value", label = "Value again", kind = "Number", required = true },
                },
            });

        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);

        var error = Assert.Single(CalculatorApi.Details(snapshot), item => item.Field == "inputs");
        Assert.Contains("used more than once", error.Message);
    }

    [Fact]
    public async Task A_malformed_slug_is_rejected_by_validation()
    {
        using var client = await factory.CreateStaffClientAsync(Roles.Admin);
        var api = new AuthApiClient(client);

        var snapshot = await api.PostForSnapshotAsync("/api/admin/calculators", DraftBody("Not A Slug"));

        Assert.Equal(HttpStatusCode.BadRequest, snapshot.StatusCode);
        Assert.Equal("slug", Assert.Single(CalculatorApi.Details(snapshot)).Field);
    }

    private static string NewSlug() => $"draft-{Guid.NewGuid():N}"[..24];

    private static object DraftBody(
        string slug,
        bool isPublished = false,
        string name = "A draft calculator",
        string summary = "A draft that staff can finish before publishing.") =>
        new
        {
            slug,
            name,
            summary,
            description = "This draft exists only to exercise the staff write path in the tests.",
            category = "Everyday",
            sortOrder = 500,
            isPublished,
            inputs = new object[]
            {
                new { key = "value", label = "Value", kind = "Number", required = true, minimum = 0, maximum = 1000 },
            },
        };

    private static Guid ReadId(string json)
    {
        using var document = JsonDocument.Parse(json);
        var id = document.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, id);

        return id;
    }

    private static async Task<PagedResponse<T>> ReadPageAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PagedResponse<T>>>(TestJson.Options);
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }
}
