using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Subscriptions;

namespace NepalHub.Tests;

/// <summary>The JSON shapes the plan endpoints return, read back with the shared test options.</summary>
internal static class SubscriptionViews
{
    internal sealed record PlanView(
        Guid Id,
        PlanTier Tier,
        string Name,
        string? Description,
        int PeriodDays,
        int PriceNpr);

    internal sealed record StateView(
        Guid BusinessId,
        string Slug,
        PlanTier? Tier,
        string? PlanName,
        DateOnly? StartsOn,
        DateOnly? EndsOn,
        bool IsActive,
        Guid? PendingRequestId);

    internal sealed record StripItemView(Guid Id, string Slug, string Name, bool IsVerified);
}

/// <summary>
/// Shared moves for the plan tests: reading and asking as the owner, deciding as staff, and
/// the handful of database facts the windows are asserted against.
/// </summary>
internal static class SubscriptionApi
{
    public const string PlansPath = "/api/plans";
    public const string RequestsPath = "/api/admin/subscription-requests";
    public const string GrantsPath = "/api/admin/subscriptions";
    public const string StripPath = "/api/businesses/featured";

    public static async Task<IReadOnlyList<SubscriptionViews.PlanView>> ReadPlansAsync(HttpClient client)
    {
        using var response = await client.GetAsync(PlansPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<SubscriptionViews.PlanView>>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    public static async Task<SubscriptionViews.StateView> GetStateAsync(HttpClient client, string slug)
    {
        using var response = await client.GetAsync($"/api/account/businesses/{slug}/subscription");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<SubscriptionViews.StateView>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    /// <summary>Asks for a plan and asserts the ask was accepted, returning the standing it reports.</summary>
    public static async Task<SubscriptionViews.StateView> SubscribeAsync(
        HttpClient owner,
        string slug,
        PlanTier tier,
        string? note = null)
    {
        var snapshot = await new AuthApiClient(owner).PostForSnapshotAsync(
            $"/api/account/businesses/{slug}/subscription-requests",
            new { tier, note });

        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        return Read<SubscriptionViews.StateView>(snapshot);
    }

    public static async Task<Moderations.PageView<AdminSubscriptionRequestResponse>> ReadRequestsAsync(
        HttpClient client,
        string query = "")
    {
        using var response = await client.GetAsync($"{RequestsPath}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<Moderations.PageView<AdminSubscriptionRequestResponse>>>(
                TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    public static async Task<SubscriptionViews.StateView> GrantAsync(
        HttpClient admin,
        Guid businessId,
        PlanTier tier)
    {
        var snapshot = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{GrantsPath}/grant",
            new { businessId, tier });

        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        return Read<SubscriptionViews.StateView>(snapshot);
    }

    public static async Task<SubscriptionViews.StateView> CancelAsync(HttpClient admin, Guid businessId)
    {
        var snapshot = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{GrantsPath}/{businessId}/cancel",
            null);

        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        return Read<SubscriptionViews.StateView>(snapshot);
    }

    /// <summary>Today's date as the service derives it, so windows line up with the server's arithmetic.</summary>
    public static DateOnly Today(NepalHubApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<INepalDateProvider>().Today;
    }

    public static async Task<int> PlanPeriodDaysAsync(NepalHubApiFactory factory, PlanTier tier)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plan = await dbContext.Plans.AsNoTracking()
            .SingleAsync(item => item.Tier == tier);

        return plan.PeriodDays;
    }

    public static async Task<int> ActiveSubscriptionCountAsync(NepalHubApiFactory factory, Guid businessId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await dbContext.BusinessSubscriptions.CountAsync(
            item => item.BusinessId == businessId && item.Status == BusinessSubscriptionStatus.Active);
    }

    public static async Task<Guid> ActiveSubscriptionIdAsync(NepalHubApiFactory factory, Guid businessId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscription = await dbContext.BusinessSubscriptions
            .SingleAsync(item => item.BusinessId == businessId
                && item.Status == BusinessSubscriptionStatus.Active);

        return subscription.Id;
    }

    public static async Task SetPlanActiveAsync(NepalHubApiFactory factory, PlanTier tier, bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plan = await dbContext.Plans.SingleAsync(item => item.Tier == tier);
        plan.IsActive = isActive;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Every other test class shares this database, so a strip test starts from no live
    /// windows at all rather than hoping nobody left one behind.
    /// </summary>
    public static async Task CancelAllActiveSubscriptionsAsync(NepalHubApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var active = await dbContext.BusinessSubscriptions
            .Where(item => item.Status == BusinessSubscriptionStatus.Active)
            .ToListAsync();

        foreach (var subscription in active)
        {
            subscription.Status = BusinessSubscriptionStatus.Cancelled;
            subscription.CancelledAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
    }

    private static T Read<T>(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<T>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }
}

/// <summary>The catalogue anybody can read before they have an account.</summary>
public sealed class PlanCatalogueTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task The_public_catalogue_lists_both_plans_cheapest_first_without_an_account()
    {
        using var client = factory.CreateClient();
        var plans = await SubscriptionApi.ReadPlansAsync(client);

        Assert.Contains(plans, plan => plan.Tier == PlanTier.Free);
        Assert.Contains(plans, plan => plan.Tier == PlanTier.Featured);
        Assert.All(plans, plan => Assert.True(plan.PeriodDays > 0));

        for (var index = 0; index < plans.Count - 1; index++)
        {
            Assert.True(plans[index].PriceNpr <= plans[index + 1].PriceNpr);
        }

        var free = plans.Single(plan => plan.Tier == PlanTier.Free);
        Assert.Equal(0, free.PriceNpr);
        Assert.False(string.IsNullOrWhiteSpace(free.Name));
    }
}

/// <summary>
/// The owner's side of the plans: free opens on the spot, featured waits in the queue, and
/// both answer as the listing's standing rather than as a separate receipt.
/// </summary>
public sealed class OwnerSubscriptionTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task The_free_plan_is_granted_on_the_spot_for_an_owned_listing()
    {
        var (email, businessId, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var today = SubscriptionApi.Today(factory);
        var period = await SubscriptionApi.PlanPeriodDaysAsync(factory, PlanTier.Free);

        var state = await SubscriptionApi.SubscribeAsync(owner, slug, PlanTier.Free);

        Assert.Equal(PlanTier.Free, state.Tier);
        Assert.Equal(today, state.StartsOn);
        Assert.Equal(today.AddDays(period), state.EndsOn);
        Assert.True(state.IsActive);
        Assert.Null(state.PendingRequestId);
        Assert.Equal(1, await SubscriptionApi.ActiveSubscriptionCountAsync(factory, businessId));

        // A grant is staff-adjacent enough to be audited even though the owner asked for it.
        var subscriptionId = await SubscriptionApi.ActiveSubscriptionIdAsync(factory, businessId);
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var audit = await Moderations.ReadAuditAsync(admin, "?action=subscription.granted&pageSize=100");

        var entry = Assert.Single(
            audit.Data!.Items,
            row => row.TargetId == subscriptionId.ToString());
        Assert.Equal("business_subscription", entry.TargetType);
        Assert.Contains("Free", entry.After);
    }

    [Fact]
    public async Task Asking_for_the_free_plan_again_rolls_one_window_forward()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var today = SubscriptionApi.Today(factory);
        var period = await SubscriptionApi.PlanPeriodDaysAsync(factory, PlanTier.Free);

        await SubscriptionApi.SubscribeAsync(owner, slug, PlanTier.Free);
        var second = await SubscriptionApi.SubscribeAsync(owner, slug, PlanTier.Free);

        Assert.Equal(today.AddDays(period * 2), second.EndsOn);
        Assert.True(second.IsActive);
        Assert.Equal(1, await SubscriptionApi.ActiveSubscriptionCountAsync(
            factory, second.BusinessId));
    }

    [Fact]
    public async Task The_featured_plan_lands_in_the_staff_queue_and_asks_only_once()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var requested = await SubscriptionApi.SubscribeAsync(owner, slug, PlanTier.Featured, "Top spot please");

        Assert.Null(requested.Tier);
        Assert.False(requested.IsActive);
        Assert.NotNull(requested.PendingRequestId);

        var again = await new AuthApiClient(owner).PostForSnapshotAsync(
            $"/api/account/businesses/{slug}/subscription-requests",
            new { tier = PlanTier.Featured, note = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("request_pending", again.Error!.Code);

        // The queue is admin reading work: a member and a moderator both stop at the door.
        using var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("stranger"));
        using var refused = await member.GetAsync(SubscriptionApi.RequestsPath);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        using var moderatorRefused = await moderator.GetAsync(SubscriptionApi.RequestsPath);
        Assert.Equal(HttpStatusCode.Forbidden, moderatorRefused.StatusCode);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        using var nonsense = await admin.GetAsync($"{SubscriptionApi.RequestsPath}?status=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
        Assert.Contains("status_invalid", await nonsense.Content.ReadAsStringAsync());

        var queue = await SubscriptionApi.ReadRequestsAsync(admin, "?pageSize=100");

        var row = Assert.Single(
            queue.Items,
            item => item.Id == requested.PendingRequestId);
        Assert.Equal(slug, row.Slug);
        Assert.Equal(email, row.RequesterEmail);
        Assert.Equal("Top spot please", row.Note);
        Assert.Equal(PlanTier.Featured, row.Tier);
        Assert.Equal(SubscriptionRequestStatus.Pending, row.Status);
    }

    [Fact]
    public async Task Approving_a_request_opens_the_featured_window_exactly_once()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);
        var requested = await SubscriptionApi.SubscribeAsync(owner, slug, PlanTier.Featured);

        var today = SubscriptionApi.Today(factory);
        var period = await SubscriptionApi.PlanPeriodDaysAsync(factory, PlanTier.Featured);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var approve = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.RequestsPath}/{requested.PendingRequestId}/approve",
            new { reviewNote = "Paid offline." });

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var state = await SubscriptionApi.GetStateAsync(owner, slug);
        Assert.Equal(PlanTier.Featured, state.Tier);
        Assert.Equal(today, state.StartsOn);
        Assert.Equal(today.AddDays(period), state.EndsOn);
        Assert.True(state.IsActive);
        Assert.Null(state.PendingRequestId);

        var requestId = requested.PendingRequestId!.Value;
        var audit = await Moderations.ReadAuditAsync(
            admin, "?action=subscription_request.approved&pageSize=100");
        var entry = Assert.Single(audit.Data!.Items, row => row.TargetId == requestId.ToString());
        Assert.Equal("subscription_request", entry.TargetType);
        Assert.Contains("Approved", entry.After);

        // One decision, then the queue stops answering the same question.
        var second = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.RequestsPath}/{requestId}/approve",
            new { reviewNote = (string?)null });
        var reject = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.RequestsPath}/{requestId}/reject",
            new { reviewNote = (string?)null });
        var unknown = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.RequestsPath}/{Guid.NewGuid()}/approve",
            new { reviewNote = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("request_closed", second.Error!.Code);
        Assert.Equal(HttpStatusCode.Conflict, reject.StatusCode);
        Assert.Equal("request_closed", reject.Error!.Code);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("request_not_found", unknown.Error!.Code);
    }

    [Fact]
    public async Task Rejecting_a_request_leaves_the_listing_without_a_window()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);
        var requested = await SubscriptionApi.SubscribeAsync(owner, slug, PlanTier.Featured);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var reject = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.RequestsPath}/{requested.PendingRequestId}/reject",
            new { reviewNote = "Waiting on payment." });

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var state = await SubscriptionApi.GetStateAsync(owner, slug);
        Assert.Null(state.Tier);
        Assert.False(state.IsActive);
        Assert.Null(state.PendingRequestId);
    }

    [Fact]
    public async Task A_plan_switched_off_cannot_be_granted_by_any_path()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);
        var business = await OwnershipApi.CreateBusinessAsync(factory);

        try
        {
            await SubscriptionApi.SetPlanActiveAsync(factory, PlanTier.Free, isActive: false);
            await SubscriptionApi.SetPlanActiveAsync(factory, PlanTier.Featured, isActive: false);

            var free = await new AuthApiClient(owner).PostForSnapshotAsync(
                $"/api/account/businesses/{slug}/subscription-requests",
                new { tier = PlanTier.Free, note = (string?)null });
            var featured = await new AuthApiClient(owner).PostForSnapshotAsync(
                $"/api/account/businesses/{slug}/subscription-requests",
                new { tier = PlanTier.Featured, note = (string?)null });

            Assert.Equal(HttpStatusCode.Conflict, free.StatusCode);
            Assert.Equal("plan_unavailable", free.Error!.Code);
            Assert.Equal(HttpStatusCode.Conflict, featured.StatusCode);
            Assert.Equal("plan_unavailable", featured.Error!.Code);

            using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
            var grant = await new AuthApiClient(admin).PostForSnapshotAsync(
                $"{SubscriptionApi.GrantsPath}/grant",
                new { businessId = business.Id, tier = PlanTier.Featured });

            Assert.Equal(HttpStatusCode.Conflict, grant.StatusCode);
            Assert.Equal("plan_unavailable", grant.Error!.Code);
        }
        finally
        {
            await SubscriptionApi.SetPlanActiveAsync(factory, PlanTier.Free, isActive: true);
            await SubscriptionApi.SetPlanActiveAsync(factory, PlanTier.Featured, isActive: true);
        }
    }

    [Fact]
    public async Task Somebody_elses_listing_answers_the_same_404_as_one_that_does_not_exist()
    {
        var (_, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var stranger = factory.CreateSignedInClientAsync(TestData.UniqueEmail("stranger"));
        var api = new AuthApiClient(stranger);

        using var read = await api.GetRawAsync($"/api/account/businesses/{slug}/subscription");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Contains("business_not_found", await read.Content.ReadAsStringAsync());

        var ask = await api.PostForSnapshotAsync(
            $"/api/account/businesses/{slug}/subscription-requests",
            new { tier = PlanTier.Free, note = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, ask.StatusCode);
        Assert.Equal("business_not_found", ask.Error!.Code);

        using var anonymous = factory.CreateClient();
        using var noSession = await anonymous.GetAsync($"/api/account/businesses/{slug}/subscription");
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
    }
}

/// <summary>
/// The admin side: direct grants, the extension rule and cancellations, reserved for
/// administrators because a grant is the closest thing this phase has to taking money.
/// </summary>
public sealed class AdminSubscriptionTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task A_direct_grant_opens_a_window_and_a_second_grant_extends_the_same_row()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);

        var today = SubscriptionApi.Today(factory);
        var period = await SubscriptionApi.PlanPeriodDaysAsync(factory, PlanTier.Featured);

        var granted = await SubscriptionApi.GrantAsync(admin, business.Id, PlanTier.Featured);

        Assert.Equal(PlanTier.Featured, granted.Tier);
        Assert.Equal(today, granted.StartsOn);
        Assert.Equal(today.AddDays(period), granted.EndsOn);
        Assert.True(granted.IsActive);
        Assert.Equal(1, await SubscriptionApi.ActiveSubscriptionCountAsync(factory, business.Id));

        var subscriptionId = await SubscriptionApi.ActiveSubscriptionIdAsync(factory, business.Id);
        var audit = await Moderations.ReadAuditAsync(admin, "?action=subscription.granted&pageSize=100");
        var entry = Assert.Single(audit.Data!.Items, row => row.TargetId == subscriptionId.ToString());
        Assert.Contains("Featured", entry.After);

        var extended = await SubscriptionApi.GrantAsync(admin, business.Id, PlanTier.Featured);

        Assert.Equal(today.AddDays(period * 2), extended.EndsOn);
        Assert.Equal(1, await SubscriptionApi.ActiveSubscriptionCountAsync(factory, business.Id));
    }

    [Fact]
    public async Task A_grant_for_an_unknown_listing_is_refused_and_a_moderator_may_not_grant()
    {
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var unknown = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.GrantsPath}/grant",
            new { businessId = Guid.NewGuid(), tier = PlanTier.Featured });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("business_not_found", unknown.Error!.Code);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        var refused = await new AuthApiClient(moderator).PostForSnapshotAsync(
            $"{SubscriptionApi.GrantsPath}/grant",
            new { businessId = business.Id, tier = PlanTier.Featured });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task Cancelling_stops_the_window_and_the_second_cancel_is_refused()
    {
        var business = await OwnershipApi.CreateBusinessAsync(factory);
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        await SubscriptionApi.GrantAsync(admin, business.Id, PlanTier.Featured);

        var cancelled = await SubscriptionApi.CancelAsync(admin, business.Id);

        Assert.Null(cancelled.Tier);
        Assert.False(cancelled.IsActive);
        Assert.Equal(0, await SubscriptionApi.ActiveSubscriptionCountAsync(factory, business.Id));

        var again = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.GrantsPath}/{business.Id}/cancel",
            null);
        var unknown = await new AuthApiClient(admin).PostForSnapshotAsync(
            $"{SubscriptionApi.GrantsPath}/{Guid.NewGuid()}/cancel",
            null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("subscription_not_active", again.Error!.Code);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("business_not_found", unknown.Error!.Code);

        using var moderator = await factory.CreateStaffClientAsync(Roles.Moderator);
        using var refused = await new AuthApiClient(moderator).PostAsync(
            $"{SubscriptionApi.GrantsPath}/{business.Id}/cancel",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}

/// <summary>
/// The strip above the directory: six paid placements, alphabetical, and nothing that has
/// not actually bought the tier — a free listing in the strip would give away the one thing
/// the paid plan sells.
/// </summary>
public sealed class FeaturedStripTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task The_strip_shows_at_most_six_live_featured_listings_in_name_order()
    {
        await SubscriptionApi.CancelAllActiveSubscriptionsAsync(factory);

        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);
        var staffApi = new AuthApiClient(staff);

        var featured = new List<(Guid Id, string Name)>();

        for (var index = 1; index <= 7; index++)
        {
            var name = $"Featured Listing {index:D2}";
            var slug = BusinessApi.NewSlug();
            var created = await staffApi.PostForSnapshotAsync(
                "/api/admin/businesses",
                BusinessApi.Body(
                    slug,
                    name,
                    published: true,
                    phone: "9800000302",
                    isVerified: true));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var business = BusinessApi.ReadCreated(created);
            await SubscriptionApi.GrantAsync(staff, business.Id, PlanTier.Featured);
            featured.Add((business.Id, name));
        }

        // Decoys that look eligible on one axis but not on all of them.
        var unverified = await CreateDecoyAsync(staff, published: true, isVerified: false, PlanTier.Featured);
        var draft = await CreateDecoyAsync(staff, published: false, isVerified: true, PlanTier.Featured);
        var freeTier = await CreateDecoyAsync(staff, published: true, isVerified: true, PlanTier.Free);
        var noSubscription = await CreateDecoyAsync(staff, published: true, isVerified: true, tier: null);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(SubscriptionApi.StripPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content
            .ReadFromJsonAsync<ApiEnvelope<IReadOnlyList<SubscriptionViews.StripItemView>>>(TestJson.Options);
        Assert.NotNull(envelope);

        var items = envelope!.Data!;

        // Six of the seven, alphabetically: the seventh loses only to the cap.
        var expectedNames = featured
            .Select(item => item.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Take(6)
            .ToList();

        Assert.Equal(expectedNames, items.Select(item => item.Name).ToList());
        Assert.All(items, item => Assert.True(item.IsVerified));
        Assert.DoesNotContain(items, row => row.Id == unverified.Id);
        Assert.DoesNotContain(items, row => row.Id == draft.Id);
        Assert.DoesNotContain(items, row => row.Id == freeTier.Id);
        Assert.DoesNotContain(items, row => row.Id == noSubscription.Id);
    }

    /// <summary>A published or draft listing with an optional window, for the strip's exclusions.</summary>
    private static async Task<(Guid Id, string Name)> CreateDecoyAsync(
        HttpClient admin,
        bool published,
        bool isVerified,
        PlanTier? tier)
    {
        // Publishing an unchecked listing is refused outright, so the unverified decoy is
        // published first and unverified afterwards — which is also how staff do it, and the
        // listing stays public either way.
        var created = await new AuthApiClient(admin).PostForSnapshotAsync(
            "/api/admin/businesses",
            BusinessApi.Body(
                BusinessApi.NewSlug(),
                $"Strip Decoy {Guid.NewGuid():N}",
                published,
                phone: "9800000303",
                isVerified: published || isVerified));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var business = BusinessApi.ReadCreated(created);

        if (published && !isVerified)
        {
            var unverified = await new AuthApiClient(admin).PutForSnapshotAsync(
                $"/api/admin/businesses/{business.Id}",
                BusinessApi.Body(
                    business.Slug,
                    business.Name,
                    published: true,
                    phone: "9800000303",
                    isVerified: false));
            Assert.Equal(HttpStatusCode.OK, unverified.StatusCode);
        }

        if (tier is { } selected)
        {
            await SubscriptionApi.GrantAsync(admin, business.Id, selected);
        }

        return (business.Id, business.Name);
    }
}
