namespace NepalHub.Api.Data.Entities;

/// <summary>
/// What a badge measures. Every value reads a fact the quiz already records, so awarding
/// one never invents state and never needs an activity table of its own.
/// </summary>
public enum BadgeCriteria
{
    /// <summary>How many daily quizzes the person has finished.</summary>
    QuizzesCompleted = 1,

    /// <summary>How many days in a row they have finished one.</summary>
    ConsecutiveDayStreak = 2,
}

/// <summary>
/// One badge the platform can award, as reference data rather than something a player or
/// a staff member creates. The three rows are seeded by the migration that creates this
/// table, so a fresh database and a migrated one hold exactly the same definitions.
///
/// The criteria are a closed enum plus a threshold rather than a JSON blob. A generic
/// rule document would let a badge be written that nothing evaluates, and the failure
/// would only surface as a badge that never arrives. Three named cases, each with a
/// matching branch in the award service, cannot drift that way.
/// </summary>
public sealed class BadgeDefinition
{
    public Guid Id { get; set; }

    /// <summary>Stable, human-readable identifier, safe to put in a URL or a test.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// A short token rather than a stored image, so the row carries no binary payload and
    /// the client decides how to draw it.
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    public BadgeCriteria Criteria { get; set; }

    /// <summary>How much of <see cref="Criteria"/> is needed.</summary>
    public int Threshold { get; set; }

    /// <summary>Presentation order on the account page.</summary>
    public int SortOrder { get; set; }
}
