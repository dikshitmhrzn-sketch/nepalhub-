using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Badges;

/// <summary>
/// Grants the badges a completed quiz has just earned, and serves the account page's list.
///
/// Granting happens after the quiz transaction has committed, never inside it. Two
/// reasons. The quiz result is the thing that matters, so a problem while writing a badge
/// must not be able to roll back a quiz the person actually finished. And the submit path
/// already catches any MySQL duplicate-key error to turn a lost race on the unique
/// (user, daily quiz) index into "you have already played today's quiz"; a second insert
/// in that same save would land in that same handler and be reported as a quiz problem.
/// Keeping the two writes apart means each one's failure is its own.
///
/// Because every badge row is derived state over the attempts, granting is idempotent by
/// construction: the only thing recorded is the award, and a badge the person already has
/// is skipped. A user with no earned badge simply has nothing to grant.
/// </summary>
public sealed class BadgeAwardService(AppDbContext dbContext)
{
    /// <summary>
    /// Records every badge this person has earned but does not yet hold, and returns the
    /// new award count. Safe to call after any persisted quiz; safe to call twice.
    ///
    /// The caller passes the attempt count and streak it has just written rather than
    /// letting this read them back, so the totals are the ones the quiz actually
    /// committed. Awarding is not allowed to fail the quiz: a database error here
    /// propagates to the caller, which is expected, because silently swallowing one would
    /// mean a badge that never arrives and nothing to show for it.
    /// </summary>
    public async Task<int> GrantEarnedBadgesAsync(
        Guid userId,
        int quizzesCompleted,
        int streakCount,
        DateTime awardedAt,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.Badges
            .AsNoTracking()
            .OrderBy(definition => definition.SortOrder)
            .ToListAsync(cancellationToken);

        var alreadyHeld = await dbContext.UserBadges
            .AsNoTracking()
            .Where(award => award.UserId == userId)
            .Select(award => award.BadgeDefinitionId)
            .ToListAsync(cancellationToken);

        var missing = definitions
            .Where(definition => !alreadyHeld.Contains(definition.Id))
            .Where(definition => BadgeRules.IsEarned(definition, quizzesCompleted, streakCount))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        foreach (var definition in missing)
        {
            dbContext.UserBadges.Add(new UserBadgeAward
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BadgeDefinitionId = definition.Id,
                AwardedAt = awardedAt,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return missing.Count;
    }

    /// <summary>
    /// Every badge, in display order, with this person's progress against each one.
    /// </summary>
    public async Task<AccountBadgesResponse> GetBadgesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var definitions = await dbContext.Badges
            .AsNoTracking()
            .OrderBy(definition => definition.SortOrder)
            .ToListAsync(cancellationToken);

        var held = await dbContext.UserBadges
            .AsNoTracking()
            .Where(award => award.UserId == userId)
            .ToListAsync(cancellationToken);

        var heldByBadge = held.ToDictionary(award => award.BadgeDefinitionId, award => award.AwardedAt);

        // Both totals come from the quiz's own records, so the fraction shown against a
        // locked badge is the same number the quiz is grading.
        var quizzesCompleted = await dbContext.QuizAttempts
            .AsNoTracking()
            .CountAsync(attempt => attempt.UserId == userId, cancellationToken);

        var streakCount = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.StreakCount)
            .FirstOrDefaultAsync(cancellationToken);

        var badges = definitions
            .Select(definition =>
            {
                // TryGetValue rather than GetValueOrDefault: the latter returns
                // default(DateTime) for a miss, which is MinValue rather than null and
                // would report every locked badge as earned on this date.
                var awarded = heldByBadge.TryGetValue(definition.Id, out var awardedAt);

                return new AccountBadgeResponse(
                    definition.Code,
                    definition.Name,
                    definition.Description,
                    definition.Icon,
                    awarded,
                    awarded ? awardedAt : null,
                    CurrentFor(definition, quizzesCompleted, streakCount),
                    definition.Threshold);
            })
            .ToList();

        return new AccountBadgesResponse(badges, badges.Count(badge => badge.Awarded));
    }

    /// <summary>
    /// Progress toward one badge, in the unit its own criterion is measured in: quizzes
    /// finished for the count, days for the streak.
    /// </summary>
    private static int CurrentFor(BadgeDefinition definition, int quizzesCompleted, int streakCount) =>
        definition.Criteria switch
        {
            BadgeCriteria.QuizzesCompleted => quizzesCompleted,
            BadgeCriteria.ConsecutiveDayStreak => streakCount,
            _ => 0,
        };
}
