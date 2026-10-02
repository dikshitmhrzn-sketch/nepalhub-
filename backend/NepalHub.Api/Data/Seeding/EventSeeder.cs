using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal static class EventSeeder
{
    /// <summary>
    /// The hour of day a seeded event is set to. Not random and not derived from anything: a
    /// calendar where every event began at whatever time the machine happened to restart would read
    /// as broken, and one that began in the middle of the night would not be attended.
    /// </summary>
    private const int DefaultStartHour = 10;

    /// <summary>
    /// The minute of the hour, always zero. A seeder that invented :37 would be showing off.
    /// </summary>
    private const int DefaultStartMinute = 0;

    /// <summary>
    /// Inserts any shipped event that is not in the database yet, matched on its slug rather than its
    /// title, so a staff edit of a seeded listing is not undone on the next restart. Rows that already
    /// exist are never touched.
    ///
    /// The ids come from <see cref="ReferenceDataIds.Event"/>, so a re-run after a partial failure
    /// converges on the same keys instead of colliding.
    ///
    /// <paramref name="today"/> is passed in rather than read from the clock, because it has to be
    /// the same Nepal date the service uses to decide what has finished. If the seeder computed its
    /// own date it could disagree with the running application by a day and seed an event that had
    /// already happened.
    ///
    /// <para>
    /// The seeder writes rows directly, so it is the one place that can create a published event whose
    /// end time has already passed. That is deliberate: it models a listing that was legitimately
    /// published and has since finished, which is exactly the state the public filter has to cope
    /// with. The service would refuse to publish such a row.
    /// </para>
    /// </summary>
    internal static async Task SeedAsync(
        AppDbContext dbContext,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var known = (await dbContext.Events
            .AsNoTracking()
            .Select(calendarEvent => calendarEvent.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // Resolved once rather than per row: the codes come from the same seeded reference data the
        // public reference endpoints serve, so a typo here is a missing district rather than a foreign
        // key error at startup.
        var districts = await dbContext.Districts
            .AsNoTracking()
            .ToDictionaryAsync(district => district.Code, StringComparer.Ordinal, cancellationToken);

        // Every business slug named by the seed set is resolved in one pass, and a slug that matches
        // nothing leaves the row unlinked rather than failing the seed: an event can easily be listed
        // before the business behind it is.
        var businessSlugs = EventSeedData.All
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

        foreach (var definition in EventSeedData.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var eventId = ReferenceDataIds.Event(definition.Slug);
            if (!known.Add(eventId))
            {
                continue;
            }

            if (!districts.TryGetValue(definition.DistrictCode, out var district))
            {
                throw new InvalidOperationException(
                    $"The seeded event '{definition.Slug}' names district '{definition.DistrictCode}', "
                    + "which is not in the reference data.");
            }

            Guid? businessId = null;
            if (definition.BusinessSlug is { } namedBusiness && businessIds.TryGetValue(namedBusiness, out var found))
            {
                businessId = found;
            }

            var startsAt = ResolveStart(definition, today);

            dbContext.Events.Add(new Event
            {
                Id = eventId,
                Slug = definition.Slug,
                Title = definition.Title,
                Summary = definition.Summary,
                Description = definition.Description,
                Category = definition.Category,
                Organiser = definition.Organiser,
                BusinessId = businessId,
                DistrictId = district.Id,
                Venue = definition.Venue,
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(Math.Max(1, definition.DurationHours)),
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
    /// Resolves the start from whichever offset the row carries, at a fixed hour of the day. A
    /// finished event is placed far enough in the past that its duration cannot run past now, so the
    /// seeded "already over" rows are unambiguously over.
    /// </summary>
    private static DateTime ResolveStart(EventSeed definition, DateOnly today)
    {
        var dayOffset = definition.ExpiredDaysAgo is { } daysAgo
            ? -daysAgo
            : definition.StartsInDays;

        return today.AddDays(dayOffset).ToDateTime(
            new TimeOnly(DefaultStartHour, DefaultStartMinute));
    }
}