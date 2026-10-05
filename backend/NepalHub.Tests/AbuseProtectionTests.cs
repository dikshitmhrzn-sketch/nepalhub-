using System.Net;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Tests;

/// <summary>
/// The abuse ceilings, each on a host whose quota is three.
///
/// A quota test has to own its host: the limiter counts per address, every client in the
/// suite arrives from the same one, and a host shared with other tests would let somebody
/// else's request eat the budget before this test got started. These two classes are
/// therefore also the two that pin their fixtures.
/// </summary>

/// <summary>
/// The report filing quota. The first complaints are ordinary filings; the fourth is
/// refused by the limiter without the endpoint ever seeing it, which is the whole point
/// — the queue never fills up with the flood in the first place.
/// </summary>
public sealed class ReportAbuseTests(ReportLimitedApiFactory factory)
    : IClassFixture<ReportLimitedApiFactory>
{
    [Fact]
    public async Task A_flood_of_reports_stops_at_the_quota()
    {
        var client = factory.CreateSignedInClientAsync(TestData.UniqueEmail("flood"));
        var api = new AuthApiClient(client);

        for (var filing = 0; filing < ReportLimitedApiFactory.PermitLimit; filing++)
        {
            var (jobId, _) = await Moderations.FirstVisibleJobAsync(factory.Services, skip: filing);

            using var filed = await api.PostAsync(
                "/api/reports",
                Moderations.ReportBody(ReportTargetType.Job, jobId));

            Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        }

        var (nextJob, _) = await Moderations.FirstVisibleJobAsync(
            factory.Services, skip: ReportLimitedApiFactory.PermitLimit);

        var refused = await api.PostForSnapshotAsync(
            "/api/reports",
            Moderations.ReportBody(ReportTargetType.Job, nextJob));

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("rate_limited", refused.Error!.Code);
    }
}

/// <summary>
/// The sign-out quota. Signing out is idempotent and the endpoint answers 200 whether or
/// not there was a session to end, so the loop below is exactly what a script would do,
/// and the ceiling lands on the same call it would.
/// </summary>
public sealed class LogoutAbuseTests(LogoutLimitedApiFactory factory)
    : IClassFixture<LogoutLimitedApiFactory>
{
    [Fact]
    public async Task Repeated_sign_outs_stop_at_the_quota()
    {
        using var client = factory.CreateClient();
        var api = new AuthApiClient(client);

        for (var attempt = 0; attempt < LogoutLimitedApiFactory.PermitLimit; attempt++)
        {
            using var signedOut = await api.LogoutAsync();
            Assert.Equal(HttpStatusCode.OK, signedOut.StatusCode);
        }

        var refused = await api.PostForSnapshotAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("rate_limited", refused.Error!.Code);
    }
}
