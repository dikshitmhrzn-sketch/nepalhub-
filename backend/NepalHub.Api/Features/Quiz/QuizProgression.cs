namespace NepalHub.Api.Features.Quiz;

/// <summary>
/// The XP and level rules, as pure arithmetic with no database and no clock, so the
/// numbers can be asserted directly rather than inferred from a response body.
///
/// The level curve is deliberately integer-only. A closed-form square root would be
/// shorter but puts a rounding boundary between two users with the same XP, and a
/// two-person discrepancy on a leaderboard is a support ticket nobody enjoys.
/// </summary>
public static class QuizProgression
{
    /// <summary>Total XP needed to reach <paramref name="level"/>. Level 1 starts at zero.</summary>
    public static int XpForLevel(int level)
    {
        if (level <= 1)
        {
            return 0;
        }

        return 50 * level * (level - 1);
    }

    /// <summary>The level a total of <paramref name="xp"/> XP earns.</summary>
    public static int LevelForXp(int xp)
    {
        if (xp <= 0)
        {
            return 1;
        }

        var level = 1;
        while (XpForLevel(level + 1) <= xp)
        {
            level++;
        }

        return level;
    }

    /// <summary>XP still owed before the next level. Zero on the highest level reached so far.</summary>
    public static int XpToNextLevel(int xp)
    {
        var next = XpForLevel(LevelForXp(xp) + 1);

        return next <= xp ? 0 : next - xp;
    }

    /// <summary>
    /// XP for one finished attempt: a flat award per right answer plus a bonus for seeing
    /// the quiz through, so finishing a hard day still feels like it counted.
    /// The bonus is unconditional. Gating it on a non-zero score would mean the worst day a
    /// player has pays nothing at all, which reads as a bug rather than as a design.
    /// </summary>
    public static int XpForAttempt(int correctAnswers, int xpPerCorrectAnswer, int completionBonusXp) =>
        (Math.Max(0, correctAnswers) * xpPerCorrectAnswer) + Math.Max(0, completionBonusXp);

    /// <summary>
    /// The streak after playing today. A gap of exactly one day extends it, anything
    /// larger restarts it, and playing twice on the same day cannot change it. The
    /// same-day case is unreachable in practice because one attempt per day is unique,
    /// but returning the current value keeps the function total.
    /// </summary>
    public static int StreakAfter(DateOnly? lastActiveOn, DateOnly today, int currentStreak)
    {
        if (lastActiveOn is not { } last)
        {
            return 1;
        }

        if (last == today)
        {
            return currentStreak;
        }

        return last == today.AddDays(-1) ? currentStreak + 1 : 1;
    }
}
