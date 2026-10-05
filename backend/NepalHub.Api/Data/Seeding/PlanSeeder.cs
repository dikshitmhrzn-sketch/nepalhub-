using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal static class PlanSeeder
{
    /// <summary>
    /// Inserts the two plans the product ships with, once. Rows already in the database are
    /// never touched, so a staff price change, a rename or a deactivation all survive a
    /// restart — the seeder only ever answers "is the catalogue missing a tier".
    ///
    /// <para>
    /// The featured price is a placeholder in rupees rather than a decision: no money moves
    /// in this phase, and the number exists so the catalogue, the admin screen and a future
    /// checkout all read one row instead of three copies of it. Staff can edit it without a
    /// deployment.
    /// </para>
    /// </summary>
    internal static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var known = (await dbContext.Plans
                .AsNoTracking()
                .Select(plan => plan.Tier)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        if (known.Add(PlanTier.Free))
        {
            dbContext.Plans.Add(new Plan
            {
                Id = ReferenceDataIds.Plan(nameof(PlanTier.Free)),
                Tier = PlanTier.Free,
                Name = "Free",
                Description = "A standard listing in the directory, with no featured placement.",
                PeriodDays = 30,
                PriceNpr = 0,
                IsActive = true,
            });
        }

        if (known.Add(PlanTier.Featured))
        {
            dbContext.Plans.Add(new Plan
            {
                Id = ReferenceDataIds.Plan(nameof(PlanTier.Featured)),
                Tier = PlanTier.Featured,
                Name = "Featured",
                Description = "Your listing at the top of the directory in the labelled featured strip, for 30 days.",
                PeriodDays = 30,
                PriceNpr = 3000,
                IsActive = true,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
