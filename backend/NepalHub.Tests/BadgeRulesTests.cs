using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Badges;

namespace NepalHub.Tests;

/// <summary>
/// The badge criteria as arithmetic, with no database and no clock, so each threshold is
/// asserted on both sides rather than inferred from a response body.
/// </summary>
public class BadgeRulesTests
{
    private static BadgeDefinition Definition(BadgeCriteria criteria, int threshold) => new()
    {
        Id = Guid.NewGuid(),
        Code = criteria.ToString(),
        Name = criteria.ToString(),
        Description = "Test definition.",
        Icon = "trophy",
        Criteria = criteria,
        Threshold = threshold,
        SortOrder = 1,
    };

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(9, 10, false)]
    [InlineData(10, 10, true)]
    [InlineData(11, 10, true)]
    public void A_quiz_count_badge_is_earned_at_its_threshold(
        int quizzesCompleted,
        int threshold,
        bool expected)
    {
        Assert.Equal(expected, BadgeRules.IsEarned(
            Definition(BadgeCriteria.QuizzesCompleted, threshold),
            quizzesCompleted,
            streakCount: 0));
    }

    [Theory]
    [InlineData(0, 7, false)]
    [InlineData(6, 7, false)]
    [InlineData(7, 7, true)]
    [InlineData(8, 7, true)]
    public void A_streak_badge_is_earned_at_its_threshold(
        int streakCount,
        int threshold,
        bool expected)
    {
        Assert.Equal(expected, BadgeRules.IsEarned(
            Definition(BadgeCriteria.ConsecutiveDayStreak, threshold),
            quizzesCompleted: 0,
            streakCount));
    }

    /// <summary>
    /// The two criteria read different columns, so a high streak must not stand in for a
    /// quiz count. Without this a badge could be awarded on the wrong number.
    /// </summary>
    [Fact]
    public void The_two_criteria_are_not_interchangeable()
    {
        var byCount = Definition(BadgeCriteria.QuizzesCompleted, 10);
        var byStreak = Definition(BadgeCriteria.ConsecutiveDayStreak, 7);

        Assert.False(BadgeRules.IsEarned(byCount, quizzesCompleted: 9, streakCount: 30));
        Assert.False(BadgeRules.IsEarned(byStreak, quizzesCompleted: 30, streakCount: 6));
        Assert.True(BadgeRules.IsEarned(byCount, quizzesCompleted: 10, streakCount: 0));
        Assert.True(BadgeRules.IsEarned(byStreak, quizzesCompleted: 0, streakCount: 7));
    }
}
