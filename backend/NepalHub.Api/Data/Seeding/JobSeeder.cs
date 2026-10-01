using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal static class JobSeeder
{
    /// <summary>
    /// Inserts any shipped posting that is not in the database yet, matched on its slug rather
    /// than its title, so a staff edit of a seeded posting is not undone on the next restart.
    /// Rows that already exist are never touched.
    ///
    /// The ids come from <see cref="ReferenceDataIds.Job"/>, so a re-run after a partial failure
    /// converges on the same keys instead of colliding.
    ///
    /// <paramref name="today"/> is passed in rather than read from the clock, because it has to be
    /// the same Nepal date the service uses to decide what has closed. If the seeder computed its
    /// own date it could disagree with the running application by a day and seed a posting that
    /// was already closed.
    ///
    /// The seeder writes rows directly, so it is the one place that can create a published posting
    /// whose deadline has already passed. That is deliberate: it models a posting that was
    /// legitimately published and has since closed, which is exactly the state the public expiry
    /// filter has to cope with. The service would refuse to publish such a row.
    /// </summary>
    internal static async Task SeedAsync(
        AppDbContext dbContext,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var known = (await dbContext.Jobs
            .AsNoTracking()
            .Select(job => job.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // Resolved once rather than per row: the codes come from the same seeded reference
        // data the public reference endpoints serve, so a typo here is a missing district rather
        // than a foreign key error at startup.
        var districts = await dbContext.Districts
            .AsNoTracking()
            .ToDictionaryAsync(district => district.Code, StringComparer.Ordinal, cancellationToken);

        var added = 0;

        foreach (var definition in JobSeedData.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobId = ReferenceDataIds.Job(definition.Slug);
            if (!known.Add(jobId))
            {
                continue;
            }

            Guid? districtId = null;
            if (definition.DistrictCode is { } districtCode)
            {
                if (!districts.TryGetValue(districtCode, out var district))
                {
                    throw new InvalidOperationException(
                        $"The seeded job '{definition.Slug}' names district '{districtCode}', "
                        + "which is not in the reference data.");
                }

                districtId = district.Id;
            }

            dbContext.Jobs.Add(new Job
            {
                Id = jobId,
                Slug = definition.Slug,
                Title = definition.Title,
                Organization = definition.Organization,
                Description = definition.Description,
                Category = definition.Category,
                EmploymentType = definition.EmploymentType,
                WorkMode = definition.WorkMode,
                DistrictId = districtId,
                ApplicationUrl = definition.ApplicationUrl,
                ApplicationDeadline = ResolveDeadline(definition, today),
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
    private static DateOnly ResolveDeadline(JobSeed definition, DateOnly today)
    {
        if (definition.ExpiresInDays is { } daysAhead)
        {
            return today.AddDays(daysAhead);
        }

        if (definition.ExpiredDaysAgo is { } daysAgo)
        {
            return today.AddDays(-daysAgo);
        }

        // Every shipped posting declares one or the other. Defaulting to a week out keeps a future
        // addition from failing startup on a required column.
        return today.AddDays(7);
    }
}
