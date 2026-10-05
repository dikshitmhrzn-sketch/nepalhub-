namespace NepalHub.Api.Features.Badges;

/// <summary>
/// One badge and whether this person has it.
///
/// Locked badges are included rather than filtered out, so the account page can show what
/// is still to come. Hiding them would make the first badge arrive as an empty screen
/// with no hint that anything else exists.
/// </summary>
public sealed record AccountBadgeResponse(
    string Code,
    string Name,
    string Description,
    string Icon,
    bool Awarded,
    DateTime? AwardedAt,
    int Current,
    int Threshold);

/// <summary>The whole set, with the earned count for a heading such as "2 of 3".</summary>
public sealed record AccountBadgesResponse(
    IReadOnlyList<AccountBadgeResponse> Badges,
    int AwardedCount);
