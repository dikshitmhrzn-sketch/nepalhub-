using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NepalHub.Api.Data.Seeding;

/// <summary>
/// Whether the fictional listing seeders run for this host. Businesses, jobs, deals and events
/// are demo content: they belong in development, in the test host and on a demo deployment, and
/// must not appear in a live directory just because the API booted there.
///
/// <para>
/// The switch is <c>Seeding:DemoContent</c>. It is not present in any committed file, so the
/// default is the one place the policy lives: on unless the environment is Production. A
/// production host that wants the sample directory sets <c>Seeding__DemoContent=true</c>, and a
/// development host that wants a clean directory sets <c>false</c>.
/// </para>
///
/// <para>
/// The reference data the rest of the API depends on — roles, provinces, districts, calculator
/// definitions, plans and badges — is deliberately not part of this switch: those seeders answer
/// "is this catalogue row missing", and an empty production database without them is a broken
/// one.
/// </para>
/// </summary>
internal static class SeedingPolicy
{
    internal const string ConfigurationKey = "Seeding:DemoContent";

    internal static bool SeedsDemoContent(IConfiguration configuration, IHostEnvironment environment)
        => configuration.GetValue(ConfigurationKey, !environment.IsProduction());
}
