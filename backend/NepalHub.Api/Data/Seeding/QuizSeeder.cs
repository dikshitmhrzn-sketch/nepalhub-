using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data.Seeding;

internal static class QuizSeeder
{
    /// <summary>
    /// Inserts any shipped question that is not in the database yet, matched on its stable
    /// code rather than its text, so a staff rewording of a seeded question is not undone
    /// on the next restart. Rows that already exist are never touched.
    ///
    /// Options get deterministic ids derived from the question code and their position, so
    /// re-running the seeder after a partial failure converges instead of colliding.
    /// </summary>
    internal static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var known = (await dbContext.QuizQuestions
            .AsNoTracking()
            .Select(question => question.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var added = 0;

        foreach (var definition in QuizSeedData.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var questionId = ReferenceDataIds.Question(definition.Code);
            if (!known.Add(questionId))
            {
                continue;
            }

            var question = new QuizQuestion
            {
                Id = questionId,
                Text = definition.Text,
                Explanation = definition.Explanation,
                Category = definition.Category,
                IsPublished = true,
            };

            var position = 1;
            foreach (var option in definition.Options)
            {
                question.Options.Add(new QuizOption
                {
                    Id = ReferenceDataIds.QuestionOption(definition.Code, position),
                    QuestionId = questionId,
                    Position = position++,
                    Text = option.Text,
                    IsCorrect = option.IsCorrect,
                });
            }

            dbContext.QuizQuestions.Add(question);
            added++;
        }

        if (added == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
