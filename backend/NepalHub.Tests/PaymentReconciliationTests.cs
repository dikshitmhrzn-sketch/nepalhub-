using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Payments;
using NepalHub.Api.Features.Subscriptions;

namespace NepalHub.Tests;

/// <summary>
/// The sweep that settles checkouts nobody came back to: it asks about rows left quiet long
/// enough, leaves fresh ones alone, stays inside its bound of provider calls per pass, and
/// reaches its result through the same settlement path the signed return would have taken.
/// </summary>
public sealed class PaymentReconciliationTests(PaymentsApiFactory factory)
    : IClassFixture<PaymentsApiFactory>
{
    [Fact]
    public async Task The_sweep_settles_a_stale_checkout_through_the_normal_settlement_path()
    {
        await PurgeSweepFixturesAsync();

        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var checkout = await PaymentsApi.CheckoutAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        await BackdateAsync(checkout.Reference, TimeSpan.FromMinutes(10));

        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        var checksBefore = factory.Gateway.StatusChecks;

        var examined = await SweepAsync();

        Assert.Equal(1, examined);
        Assert.Equal(checksBefore + 1, factory.Gateway.StatusChecks);

        // The consequences are the ones a signed return would have produced: money is not
        // allowed to open a different door just because nobody was watching when it arrived.
        var state = await SubscriptionApi.GetStateAsync(owner, slug);
        Assert.Equal(PlanTier.Featured, state.Tier);
        Assert.True(state.IsActive);
        Assert.Null(state.PendingRequestId);

        var payment = await PaymentsApi.ReadPaymentAsync(owner, checkout.Reference);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.NotNull(payment.SettledAt);
    }

    [Fact]
    public async Task The_sweep_leaves_a_fresh_checkout_alone()
    {
        await PurgeSweepFixturesAsync();

        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var checkout = await PaymentsApi.CheckoutAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        // The answer would settle it if the sweep dared to ask this early — the provider is
        // still mid-transaction with the buyer, and a guess here would be a lie in either
        // direction.
        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        var checksBefore = factory.Gateway.StatusChecks;

        var examined = await SweepAsync();

        Assert.Equal(0, examined);
        Assert.Equal(checksBefore, factory.Gateway.StatusChecks);

        var payment = await PaymentsApi.ReadPaymentAsync(owner, checkout.Reference);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    /// <summary>
    /// The bound that keeps the sweep from becoming a load: at most five provider questions
    /// per pass, whatever the backlog. Six stale rows go in and exactly five are asked about;
    /// whose five is the sweep's own business (oldest first), so only the count is asserted.
    /// </summary>
    [Fact]
    public async Task The_sweep_asks_about_at_most_five_rows_per_pass()
    {
        await PurgeSweepFixturesAsync();

        var (email, _, _) = await OwnershipApi.MakeOwnerAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.SingleAsync(item => item.Email == email);

            var staleAt = DateTime.UtcNow - TimeSpan.FromMinutes(10);
            for (var index = 0; index < 6; index++)
            {
                dbContext.PaymentTransactions.Add(new PaymentTransaction
                {
                    Id = Guid.NewGuid(),
                    Reference = $"swp-{index}-{Guid.NewGuid():N}",
                    UserId = user.Id,
                    Kind = PaymentKind.Subscription,
                    AmountNpr = 100,
                    Description = "fixture for the sweep's bound",
                    Status = PaymentStatus.Pending,
                    CreatedAt = staleAt,
                    UpdatedAt = staleAt,
                });
            }

            await dbContext.SaveChangesAsync();

            // updated_at is database-generated (CURRENT_TIMESTAMP(6) on every write), so a
            // direct UPDATE is the only way to age the rows — an assigned value through EF
            // would be discarded and left fresh, invisible to the sweep.
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE payment_transactions SET updated_at = {0} WHERE reference LIKE 'swp-%'",
                staleAt);
        }

        factory.Gateway.Answer = ProviderNoticeKind.Pending;
        var checksBefore = factory.Gateway.StatusChecks;

        var examined = await SweepAsync();

        Assert.Equal(5, examined);
        Assert.Equal(checksBefore + 5, factory.Gateway.StatusChecks);
    }

    /// <summary>
    /// The three tests share one database and one sweep, so rows one test leaves behind would
    /// count toward another's numbers — and could crowd its row out of the oldest-first queue
    /// entirely. Every test therefore starts by clearing the fixture rows the last one wrote.
    /// </summary>
    private async Task PurgeSweepFixturesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM payment_transactions WHERE reference LIKE 'swp-%'");
    }

    private async Task<int> SweepAsync()
    {
        using var scope = factory.Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();
        return await paymentService.ReconcileStaleAsync(CancellationToken.None);
    }

    private async Task BackdateAsync(string reference, TimeSpan age)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // updated_at is database-generated (CURRENT_TIMESTAMP(6) on every write), so an
        // assigned value through EF would be discarded — a direct UPDATE is how the row
        // ages the way the passage of time would have aged it.
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE payment_transactions SET updated_at = {0} WHERE reference = {1}",
            DateTime.UtcNow - age,
            reference);
    }
}
