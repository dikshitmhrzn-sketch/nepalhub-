import { parseNepalWallClock } from '@/features/events/types';

import type { AccountBadge } from './types';

/**
 * The `icon` column holds a key, not a filename or an image. It is resolved here to one text
 * glyph rather than shipped as artwork, so a badge cannot become a broken image, the whole set
 * costs nothing to load, and a key nobody has drawn still renders as something.
 *
 * This is the same trade the rest of the UI makes: the brand mark is the letters `NH` and a
 * card's affordance is the character `→`. Adding an icon package for three glyphs would be a
 * dependency bought for a worse result than the characters already give.
 */
const badgeIcons: Record<string, string> = {
  trophy: '🏆',
  flame: '🔥',
  book: '📘',
};

/** A neutral mark for a key this build has no glyph for, so an unknown badge is not a blank. */
const fallbackIcon = '◈';

export function badgeIcon(icon: string): string {
  return badgeIcons[icon] ?? fallbackIcon;
}

/**
 * Progress toward one badge, as "3 of 10".
 *
 * Deliberately unitless. The API reports `current` against `threshold` but does not say which
 * criterion produced them, so "3 of 10 quizzes" would be a guess made by matching the badge's
 * code — and a badge added to the seed later would then be labelled in whatever language the
 * match table happened to contain. The description sitting directly above this line already says
 * what is being counted ("Finish ten daily quizzes."), so restating it here could only be wrong.
 *
 * An earned badge shows its own progress as complete rather than a fraction, because the number
 * that earned it is in the past.
 */
export function progressLabel(badge: AccountBadge): string {
  if (badge.awarded) {
    return 'Earned';
  }

  return `${badge.current} of ${badge.threshold}`;
}

/**
 * How full the progress meter is, as a whole percentage.
 *
 * Clamped rather than trusted: a threshold of zero would otherwise divide by zero, and a value
 * above it would ask the browser to lay out a bar wider than its track. An earned badge is
 * always full.
 */
export function progressPercent(badge: AccountBadge): number {
  if (badge.awarded) {
    return 100;
  }

  if (badge.threshold <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round((badge.current / badge.threshold) * 100)));
}

const monthNames = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
] as const;

/**
 * When a badge was earned, read as the Nepal wall clock it was written in.
 *
 * `awardedAt` arrives as a bare `2026-10-02T18:30:00` with no offset, the same shape as an
 * event's start time. `new Date(value)` would read that as the browser's own zone and move the
 * date by a different number of hours for every reader, so the parts are taken as text instead.
 */
export function formatAwardedAt(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const parsed = parseNepalWallClock(value);
  if (!parsed) {
    return value;
  }

  const month = monthNames[parsed.getMonth()];

  return `${parsed.getDate()} ${month} ${parsed.getFullYear()}`;
}
