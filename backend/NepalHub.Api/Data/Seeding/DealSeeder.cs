using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal static class DealSeeder
{
    /// <summary>
    /// Inserts any shipped offer that is not in the database yet, matched on its slug rather than
    /// its title, so a staff edit of a seeded offer is not undone on the next restart. Rows that
    /// already exist are never touched.
    ///
    /// The ids come from <see cref="ReferenceDataIds.Deal"/>, so a re-run after a partial failure
    /// converges on the same keys instead of colliding.
    ///
    /// <paramref name="today"/> is passed in rather than read from the clock, because it has to be
    /// the same Nepal date the service uses to decide what has expired. If the seeder computed its
    /// own date it could disagree with the running application by a day and seed an offer that was
    /// already over.
    ///
    /// The seeder writes rows directly, so it is the one place that can create a published offer
    /// whose end date has already passed. That is deliberate: it models an offer that was
    /// legitimately published and has since ended, which is exactly the state the public expiry
    /// filter has to cope with. The service would refuse to publish such a row.
    ///
    /// A deal may name a business without one existing yet, and that is not an error. Deals routinely
    /// predate the listing, and a seeded row pointing at a business slug that has not been created is
    /// left unlinked rather than dropped, so the offer still appears in the public list.
    /// </summary>
    internal static async Task SeedAsync(
        AppDbContext dbContext,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var known = (await dbContext.Deals
            .AsNoTracking()
            .Select(deal => deal.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // Resolved once rather than per row: the codes come from the same seeded reference data the
        // public reference endpoints serve, so a typo here is a missing district rather than a
        // foreign key error at startup.
        var districts = await dbContext.Districts
            .AsNoTracking()
            .ToDictionaryAsync(district => district.Code, StringComparer.Ordinal, cancellationToken);

        // Every business slug named by the seed set is collected first and resolved in one pass, so a
        // typo fails the whole set with a clear message rather than silently unlinking a row.
        var businessSlugs = DealSeedData.All
            .Select(definition => definition.BusinessSlug)
            .Where(slug => slug is not null)
            .Select(slug => slug!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var businessIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var businessSlug in businessSlugs)
        {
            var match = await dbContext.Businesses
                .AsNoTracking()
                .Where(business => business.Slug == businessSlug)
                .Select(business => (Guid?)business.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (match is { } businessId)
            {
                businessIds[businessSlug] = businessId;
            }
        }

        var added = 0;

        foreach (var definition in DealSeedData.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dealId = ReferenceDataIds.Deal(definition.Slug);
            if (!known.Add(dealId))
            {
                continue;
            }

            Guid? districtId = null;
            if (definition.DistrictCode is { } districtCode)
            {
                if (!districts.TryGetValue(districtCode, out var district))
                {
                    throw new InvalidOperationException(
                        $"The seeded deal '{definition.Slug}' names district '{districtCode}', "
                        + "which is not in the reference data.");
                }

                districtId = district.Id;
            }

            Guid? businessId = null;
            if (definition.BusinessSlug is { } namedBusiness && businessIds.TryGetValue(namedBusiness, out var found))
            {
                businessId = found;
            }

            dbContext.Deals.Add(new Deal
            {
                Id = dealId,
                Slug = definition.Slug,
                Title = definition.Title,
                Summary = definition.Summary,
                Description = definition.Description,
                DiscountText = definition.DiscountText,
                BusinessId = businessId,
                DistrictId = districtId,
                StartsOn = today.AddDays(definition.StartsInDays),
                EndsOn = ResolveEnd(definition, today),
                IsPublished = definition.IsPublished,
            });

            added++;
        }

        if (added == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves the end date from whichever offset the row carries. An expired row ends in the past;
    /// an active row ends in the future; and a row that carries neither would run forever, so it is
    /// refused here rather than written with a date nobody chose.
    /// </summary>
    private static DateOnly ResolveEnd(DealSeed definition, DateOnly today)
    {
        if (definition.ExpiredDaysAgo is { } daysAgo)
        {
            return today.AddDays(-daysAgo);
        }

        if (definition.EndsInDays is { } daysAhead)
        {
            return today.AddDays(daysAhead);
        }

        throw new InvalidOperationException(
            $"The seeded deal '{definition.Slug}' has neither an end date nor an expired date. An "
            + "offer with no end date would never come off the public list.");
    }
}