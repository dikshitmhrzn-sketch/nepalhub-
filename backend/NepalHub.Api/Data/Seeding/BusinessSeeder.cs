using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal static class BusinessSeeder
{
    /// <summary>
    /// Inserts any shipped listing that is not in the database yet, matched on its slug rather than
    /// its name, so a staff edit of a seeded listing is not undone on the next restart. Rows that
    /// already exist are never touched.
    ///
    /// The ids come from <see cref="ReferenceDataIds.Business"/>, so a re-run after a partial failure
    /// converges on the same keys instead of colliding.
    ///
    /// Opening hours are written alongside their parent. They are child rows with a composite key,
    /// so a business cannot exist with half a week saved, and inserting them in the same call keeps
    /// the two tables consistent even if the process dies between batches.
    ///
    /// Unlike the job seeder this one needs no date: a shop's opening hours are wall-clock times
    /// that do not expire, so there is no risk of seeding a row that was already closed by the time
    /// the application read it.
    /// </summary>
    internal static async Task SeedAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var known = (await dbContext.Businesses
            .AsNoTracking()
            .Select(business => business.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // Resolved once rather than per row: the codes come from the same seeded reference data the
        // public reference endpoints serve, so a typo here is a missing district rather than a
        // foreign key error at startup.
        var districts = await dbContext.Districts
            .AsNoTracking()
            .ToDictionaryAsync(district => district.Code, StringComparer.Ordinal, cancellationToken);

        var added = 0;

        foreach (var definition in BusinessSeedData.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var businessId = ReferenceDataIds.Business(definition.Slug);
            if (!known.Add(businessId))
            {
                continue;
            }

            if (!districts.TryGetValue(definition.DistrictCode, out var district))
            {
                throw new InvalidOperationException(
                    $"The seeded business '{definition.Slug}' names district '{definition.DistrictCode}', "
                    + "which is not in the reference data.");
            }

            var business = new Business
            {
                Id = businessId,
                Slug = definition.Slug,
                Name = definition.Name,
                Description = definition.Description,
                Category = definition.Category,
                DistrictId = district.Id,
                Address = definition.Address,
                Phone = definition.Phone,
                Email = definition.Email,
                Website = definition.Website,
                IsPublished = definition.IsPublished,
                IsVerified = definition.IsVerified,
                VerifiedAt = definition.IsVerified ? DateTime.UtcNow : null,
            };

            foreach (var day in definition.OpeningHours ?? [])
            {
                business.OpeningHours.Add(new BusinessOpeningHours
                {
                    BusinessId = businessId,
                    DayOfWeek = day.DayOfWeek,
                    OpensAt = day.OpensAt,
                    ClosesAt = day.ClosesAt,
                });
            }

            dbContext.Businesses.Add(business);

            added++;
        }

        if (added == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}