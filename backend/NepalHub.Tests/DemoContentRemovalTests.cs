using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Data.Seeding;

namespace NepalHub.Tests;

/// <summary>
/// The demo directory is removable and real listings are not. Proven against the seeded test
/// database inside a transaction that is rolled back, because the rest of the suite reads the
/// demo rows the shared host inserted and must not find them missing.
/// </summary>
public class DemoContentRemovalTests
{
    [Fact]
    public async Task Removes_seeded_demo_listings_and_leaves_real_rows_and_reference_data()
    {
        await using var dbContext = CreateDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // The shared test database was seeded with the demo directory at startup.
            Assert.True(
                await dbContext.Jobs.AnyAsync(job => job.Slug == JobSeedData.All[0].Slug),
                "the test database is expected to hold the seeded demo jobs");

            // A listing a real user would have posted: random id, slug the seed set never uses.
            var realJob = new Job
            {
                Id = Guid.NewGuid(),
                Slug = "a-real-job-posting",
                Title = "A real job",
                Organization = "A real employer",
                Description = "Posted by a person, not by the seeder.",
                Category = JobCategory.Other,
                EmploymentType = EmploymentType.FullTime,
                WorkMode = WorkMode.Remote,
                ApplicationUrl = "https://example.com/apply",
                ApplicationDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                IsPublished = true,
            };
            dbContext.Jobs.Add(realJob);
            await dbContext.SaveChangesAsync();

            await DemoContentRemoval.RemoveAsync(dbContext, CancellationToken.None);

            // Every demo slug is gone from all four tables...
            var demoJobSlugs = JobSeedData.All.Select(definition => definition.Slug).ToList();
            Assert.Equal(
                0,
                await dbContext.Jobs.CountAsync(job => demoJobSlugs.Contains(job.Slug)));

            var demoBusinessSlugs = BusinessSeedData.All.Select(definition => definition.Slug).ToList();
            Assert.Equal(
                0,
                await dbContext.Businesses.CountAsync(business => demoBusinessSlugs.Contains(business.Slug)));

            var demoDealSlugs = DealSeedData.All.Select(definition => definition.Slug).ToList();
            Assert.Equal(
                0,
                await dbContext.Deals.CountAsync(deal => demoDealSlugs.Contains(deal.Slug)));

            var demoEventSlugs = EventSeedData.All.Select(definition => definition.Slug).ToList();
            Assert.Equal(
                0,
                await dbContext.Events.CountAsync(calendarEvent => demoEventSlugs.Contains(calendarEvent.Slug)));

            // ...the real one is not...
            Assert.True(
                await dbContext.Jobs.AnyAsync(job => job.Id == realJob.Id),
                "a listing the user posted must survive the demo removal");

            // ...and the reference data the site cannot run without is untouched.
            Assert.True(await dbContext.Calculators.AnyAsync());
            Assert.True(await dbContext.Plans.AnyAsync());
            Assert.True(await dbContext.Districts.AnyAsync());
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    /// <summary>
    /// A second pass over an already-clean database must succeed and change nothing, because
    /// the removal runs on every production boot rather than once.
    /// </summary>
    [Fact]
    public async Task A_second_removal_pass_is_a_no_op()
    {
        await using var dbContext = CreateDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            await DemoContentRemoval.RemoveAsync(dbContext, CancellationToken.None);
            var realJobsAfterFirstPass = await dbContext.Jobs.CountAsync();

            await DemoContentRemoval.RemoveAsync(dbContext, CancellationToken.None);

            Assert.Equal(realJobsAfterFirstPass, await dbContext.Jobs.CountAsync());
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(
                TestDatabase.ConnectionString,
                ServerVersion.Parse("8.4.0-mysql"),
                mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
