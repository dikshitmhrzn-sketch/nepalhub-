namespace NepalHub.Api.Data.Entities;

/// <summary>
/// A badge one person has earned, with the moment they earned it.
///
/// This table is the record of the award, and the unique constraint on (user, badge) is
/// what makes granting one idempotent: the same quiz replayed, a retried request or two
/// tabs open at once cannot produce a second row, so "has this been awarded" is a question
/// the database answers rather than something the application has to guess.
///
/// The award timestamp is the instant the qualifying quiz was recorded, not the instant a
/// later backfill noticed it, so the order of the list reflects when the work happened.
/// </summary>
public sealed class UserBadgeAward
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public Guid BadgeDefinitionId { get; set; }

    public BadgeDefinition? BadgeDefinition { get; set; }

    public DateTime AwardedAt { get; set; }
}
