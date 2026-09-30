using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Calculators;

namespace NepalHub.Api.Data.Seeding;

internal static class CalculatorSeeder
{
    /// <summary>
    /// Inserts any shipped calculator that is not in the database yet. Rows that
    /// already exist are never touched, so a staff rename, a copy edit or a retraction
    /// all survive a restart.
    /// </summary>
    internal static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var slugs = await dbContext.Calculators
            .AsNoTracking()
            .Select(calculator => calculator.Slug)
            .ToListAsync(cancellationToken);

        var known = slugs.ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var definition in CalculatorSeedData.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!known.Add(definition.Slug))
            {
                continue;
            }

            dbContext.Calculators.Add(new Calculator
            {
                Id = ReferenceDataIds.Calculator(definition.Slug),
                Slug = definition.Slug,
                Name = definition.Name,
                Summary = definition.Summary,
                Description = definition.Description.Trim(),
                Category = definition.Category,
                InputsJson = CalculatorInputSchema.Serialize(definition.Inputs),
                SortOrder = definition.SortOrder,
                IsPublished = true,
            });

            added++;
        }

        if (added == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
