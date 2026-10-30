using System.Net;
using System.Net.Http.Json;
using NepalHub.Api.Features.Reference;

namespace NepalHub.Tests;

public sealed class ReferenceDataTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    private static readonly (string Code, string Name, int Districts)[] Expected =
    [
        ("koshi", "Koshi", 14),
        ("madhesh", "Madhesh", 8),
        ("bagmati", "Bagmati", 13),
        ("gandaki", "Gandaki", 11),
        ("lumbini", "Lumbini", 12),
        ("karnali", "Karnali", 10),
        ("sudurpashchim", "Sudurpashchim", 9),
    ];

    [Fact]
    public async Task The_seven_provinces_are_seeded_in_constitutional_order()
    {
        using var client = factory.CreateClient();

        var provinces = await ReadAsync<ProvinceResponse>(client, "/api/reference/provinces");

        Assert.Equal(7, provinces.Count);
        Assert.Equal(Expected.Select(province => province.Code), provinces.Select(province => province.Code));
        Assert.Equal(Expected.Select(province => province.Name), provinces.Select(province => province.Name));
    }

    [Fact]
    public async Task All_seventy_seven_districts_are_seeded_under_the_right_province()
    {
        using var client = factory.CreateClient();
        var total = 0;

        foreach (var (code, name, expectedCount) in Expected)
        {
            var districts = await ReadAsync<DistrictResponse>(client, $"/api/reference/provinces/{code}/districts");

            Assert.Equal(expectedCount, districts.Count);
            Assert.All(districts, district => Assert.StartsWith($"{code}-", district.Code));
            Assert.Equal(districts.Count, districts.Select(district => district.Name).Distinct().Count());

            total += districts.Count;
            Assert.True(expectedCount > 0, name);
        }

        Assert.Equal(77, total);
    }

    [Fact]
    public async Task Rukum_is_split_between_lumbini_and_karnali()
    {
        using var client = factory.CreateClient();

        var lumbini = await ReadAsync<DistrictResponse>(client, "/api/reference/provinces/lumbini/districts");
        var karnali = await ReadAsync<DistrictResponse>(client, "/api/reference/provinces/karnali/districts");

        Assert.Contains(lumbini, district => district.Name == "Rukum East");
        Assert.Contains(karnali, district => district.Name == "Rukum West");
    }

    [Fact]
    public async Task An_unknown_province_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/reference/provinces/nowhere/districts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<List<T>> ReadAsync<T>(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<T>>>();
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }
}

public sealed class HealthEndpointTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task Health_reports_a_connected_database()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<HealthResponse>>();
        Assert.NotNull(envelope);

        Assert.Equal("healthy", envelope!.Data!.Status);
        Assert.Equal("connected", envelope.Data.Database);
    }

    /// <summary>
    /// The probe answers before any authentication, so the raw JSON - not a parsed record
    /// that would quietly swallow extra fields - must carry nothing that names the stack:
    /// neither the database version nor the environment the host runs under.
    /// </summary>
    [Fact]
    public async Task Health_does_not_disclose_the_stack_to_an_anonymous_probe()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("serverVersion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"environment\"", json, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record HealthResponse(string Status, string Database, DateTimeOffset Timestamp);
