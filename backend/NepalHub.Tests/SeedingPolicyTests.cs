using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NepalHub.Api.Data.Seeding;

namespace NepalHub.Tests;

/// <summary>
/// The demo-seeding policy as one rule, with no host and no database, so the production default
/// is asserted directly rather than inferred from whatever a booted test host happened to insert.
/// </summary>
public class SeedingPolicyTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Staging", true)]
    [InlineData("Production", false)]
    public void Demo_listings_seed_by_default_everywhere_except_production(string environment, bool expected)
    {
        Assert.Equal(expected, SeedingPolicy.SeedsDemoContent(Configuration(), HostEnvironment(environment)));
    }

    /// <summary>
    /// A demo deployment that runs under the Production environment name still gets the sample
    /// directory when it asks for it, so the flag is a real switch and not a second environment check.
    /// </summary>
    [Fact]
    public void Production_can_ask_for_the_sample_directory_explicitly()
    {
        Assert.True(SeedingPolicy.SeedsDemoContent(Configuration("true"), HostEnvironment("Production")));
    }

    /// <summary>
    /// The mirror of the same switch: a development machine that wants a clean directory, which is
    /// how a developer preparing launch content reviews an empty catalogue.
    /// </summary>
    [Fact]
    public void Development_can_ask_for_a_clean_directory_explicitly()
    {
        Assert.False(SeedingPolicy.SeedsDemoContent(Configuration("false"), HostEnvironment("Development")));
    }

    private static IConfiguration Configuration(string? demoContent = null)
    {
        var values = demoContent is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { [SeedingPolicy.ConfigurationKey] = demoContent };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment HostEnvironment(string name) => new StubHostEnvironment(name);

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "NepalHub.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
