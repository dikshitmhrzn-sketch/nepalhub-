using Microsoft.EntityFrameworkCore;

namespace NepalHub.Api.Data.Seeding;

/// <summary>
/// Removes the demo directory from a host that does not want it. The seeders insert by
/// deterministic id (<see cref="ReferenceDataIds"/>), so the ids are the one reliable way to
/// tell a shipped sample row from a real one: a listing a user posted has a random id and is
/// never matched, however similar its slug looks.
///
/// <para>
/// Runs on every boot where demo content is off, which on a production host means every
/// deploy. The statements are primary-key lookups against ids that stop existing after the
/// first pass, so the steady-state cost is nothing, and a host that once ran with demo
/// content and later turned it off converges on an empty catalogue without anyone having to
/// remember a manual cleanup.
/// </para>
/// </summary>
internal static class DemoContentRemoval
{
    internal static async Task RemoveAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var jobIds = JobSeedData.All
            .Select(definition => ReferenceDataIds.Job(definition.Slug))
            .ToList();
        var businessIds = BusinessSeedData.All
            .Select(definition => ReferenceDataIds.Business(definition.Slug))
            .ToList();
        var dealIds = DealSeedData.All
            .Select(definition => ReferenceDataIds.Deal(definition.Slug))
            .ToList();
        var eventIds = EventSeedData.All
            .Select(definition => ReferenceDataIds.Event(definition.Slug))
            .ToList();

        // Children before parents, so no foreign key is asked to cascade in a way the order
        // could get wrong: bookings hang off events, hours off businesses, and offers and
        // events can name a business that is about to go.
        await dbContext.EventBookings
            .Where(booking => eventIds.Contains(booking.EventId))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Deals
            .Where(deal => dealIds.Contains(deal.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Events
            .Where(calendarEvent => eventIds.Contains(calendarEvent.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.BusinessOpeningHours
            .Where(hours => businessIds.Contains(hours.BusinessId))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Businesses
            .Where(business => businessIds.Contains(business.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Jobs
            .Where(job => jobIds.Contains(job.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
