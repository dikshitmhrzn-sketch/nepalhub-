/** Mirrors `BadgeContracts.cs` from the ASP.NET Core API. */

/**
 * One badge and whether this person holds it.
 *
 * Locked badges are sent rather than filtered out, so the account page can show what is still to
 * come. `current` and `threshold` are in the unit that badge's own criterion is measured in:
 * quizzes finished for the count, days for the streak.
 */
export interface AccountBadge {
  code: string;
  name: string;
  description: string;
  icon: string;
  awarded: boolean;
  awardedAt: string | null;
  current: number;
  threshold: number;
}

/** The whole set, with the earned count for a heading such as "1 of 3". */
export interface AccountBadges {
  badges: AccountBadge[];
  awardedCount: number;
}
