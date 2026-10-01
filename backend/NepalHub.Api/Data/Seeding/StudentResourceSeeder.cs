using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal static class StudentResourceSeeder
{
    /// <summary>
    /// Inserts any shipped resource that is not in the database yet, matched on its stable
    /// code rather than its title, so a staff edit of a seeded entry is not undone on the next
    /// restart. Rows that already exist are never touched.
    ///
    /// The ids come from <see cref="ReferenceDataIds.Resource"/>, so a re-run after a partial
    /// failure converges on the same keys instead of colliding.
    ///
    /// <paramref name="today"/> is passed in rather than read from the clock, because it has to
    /// be the same Nepal date the service uses to decide what has expired. If the seeder computed
    /// its own date it could disagree with the running application by a day and seed a
    /// scholarship that was already closed.
    ///
    /// The seeder writes rows directly, so it is the one place that can create a published
    /// resource whose deadline has already passed. That is deliberate: it models a scholarship
    /// that was legitimately published and has since closed, which is exactly the state the
    /// public expiry filter has to cope with. The service would refuse to publish such a row.
    /// </summary>
    internal static async Task SeedAsync(
        AppDbContext dbContext,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var known = (await dbContext.StudentResources
            .AsNoTracking()
            .Select(resource => resource.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var added = 0;

        foreach (var definition in StudentResourceSeedData.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resourceId = ReferenceDataIds.Resource(definition.Code);
            if (!known.Add(resourceId))
            {
                continue;
            }

            dbContext.StudentResources.Add(new StudentResource
            {
                Id = resourceId,
                Slug = definition.Slug,
                Title = definition.Title,
                Summary = definition.Summary,
                Body = definition.Body,
                Type = definition.Type,
                Category = definition.Category,
                Level = definition.Level,
                ExternalUrl = definition.ExternalUrl,
                AcademicYear = definition.AcademicYear,
                Provider = definition.Provider,
                AwardAmountNpr = definition.AwardAmountNpr,
                Eligibility = definition.Eligibility,
                ExpiresOn = ResolveDeadline(definition, today),
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
    /// Turns a relative deadline into a date. The two offsets are mutually exclusive by
    /// construction in the seed data, and the future one wins if a row ever declares both, so a
    /// mistake shows up as a listing that stays open rather than one that vanishes immediately.
    /// </summary>
    private static DateOnly? ResolveDeadline(StudentResourceSeed definition, DateOnly today)
    {
        if (definition.ExpiresInDays is { } daysAhead)
        {
            return today.AddDays(daysAhead);
        }

        if (definition.ExpiredDaysAgo is { } daysAgo)
        {
            return today.AddDays(-daysAgo);
        }

        return null;
    }
}
