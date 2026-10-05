using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Badges;

/// <summary>
/// Whether one badge is met, as pure arithmetic over the numbers the quiz already
/// persists: a count of finished attempts and the streak currently on the user row.
///
/// The streak deliberately reads the stored <c>StreakCount</c> rather than recounting
/// days here. That value is the one the quiz wrote from
/// <see cref="Quiz.QuizProgression.StreakAfter"/>, so a badge and the streak shown on the
/// account page can never disagree about whether seven days were consecutive. Counting
/// days a second time would give this class its own idea of "consecutive", which is the
/// sort of rule that drifts a release later.
/// </summary>
public static class BadgeRules
{
    /// <summary>
    /// Whether <paramref name="definition"/> is earned by these totals. Every criterion
    /// compares against the threshold in the definition row, so adding a badge to the seed
    /// needs no change here.
    /// </summary>
    public static bool IsEarned(BadgeDefinition definition, int quizzesCompleted, int streakCount) =>
        definition.Criteria switch
        {
            BadgeCriteria.QuizzesCompleted => quizzesCompleted >= definition.Threshold,
            BadgeCriteria.ConsecutiveDayStreak => streakCount >= definition.Threshold,
            _ => false,
        };
}
