import { parseNepalWallClock } from './types';
import type { EventCategory } from './types';
import { categoryLabels } from './types';

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
 * One event time, in the Nepal wall clock it was entered in.
 *
 * The API stores these as bare local times with no zone, so they arrive as `2026-03-14T10:00:00`
 * and `new Date(value)` would be read as the browser's own zone, moving the event by a different
 * number of hours for every reader. The parts are taken as text and formatted here instead, so
 * ten o'clock reads as ten o'clock everywhere.
 */
export function formatEventTime(value: string): string {
  const parsed = parseNepalWallClock(value);

  if (!parsed) {
    return value;
  }

  const hours = parsed.getHours();
  const minutes = parsed.getMinutes();
  const meridiem = hours < 12 ? 'am' : 'pm';
  const hour = hours % 12 === 0 ? 12 : hours % 12;
  const minuteText = minutes === 0 ? '' : `:${`${minutes}`.padStart(2, '0')}`;

  return `${hour}${minuteText}${meridiem}`;
}

/** The same time with the day and month, for a card. */
export function formatEventDate(value: string): string {
  const parsed = parseNepalWallClock(value);

  if (!parsed) {
    return value;
  }

  const monthName = monthNames[parsed.getMonth()];

  return `${parsed.getDate()} ${monthName}${parsed.getFullYear() === new Date().getFullYear() ? '' : ` ${parsed.getFullYear()}`}`;
}

/**
 * The when-line on a card: the day, the hours, and whether it is happening right now.
 *
 * The range collapses when both times fall on one day, which is the usual case for an event, and
 * keeps the day repeated when it does not. An ongoing event says so, because "now" is the only
 * detail that changes a decision about whether to go.
 */
export function describeWhen(
  startsAt: string,
  endsAt: string,
  isOngoing: boolean,
): string {
  const start = parseNepalWallClock(startsAt);
  const end = parseNepalWallClock(endsAt);

  if (!start || !end) {
    return formatEventDate(startsAt);
  }

  const sameDay =
    start.getFullYear() === end.getFullYear() &&
    start.getMonth() === end.getMonth() &&
    start.getDate() === end.getDate();

  const range = sameDay
    ? `${formatEventDate(startsAt)}, ${formatEventTime(startsAt)} to ${formatEventTime(endsAt)}`
    : `${formatEventDate(startsAt)}, ${formatEventTime(startsAt)} to ${formatEventDate(endsAt)}, ${formatEventTime(endsAt)}`;

  return isOngoing ? `${range} · Happening now` : range;
}

/** Where an event is, when the venue is given. */
export function formatVenue(venue: string | null, districtName: string | null): string | null {
  if (venue && districtName) {
    return `${venue}, ${districtName}`;
  }

  return venue ?? districtName;
}

export function categoryLabel(category: EventCategory): string {
  return categoryLabels[category];
}

/**
 * Turns a title into the slug the API will accept, for the "generate from the title" button in the
 * editor. Anything that is not a lowercase letter or a digit becomes a hyphen, and runs of hyphens
 * collapse, so the result is checked against the same pattern the server enforces rather than being
 * close enough to it.
 */
export function slugifyTitle(title: string): string {
  return title
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 160);
}

/**
 * The staff row's status. Three facts rather than two, because a finished event is the one case a
 * staff member is usually looking for and "published" alone would hide it: readers cannot see it,
 * but it is not a draft either.
 */
export function describeStaffRow(row: {
  isPublished: boolean;
  isFinished: boolean;
}): 'Live' | 'Finished' | 'Draft' {
  if (!row.isPublished) {
    return 'Draft';
  }

  return row.isFinished ? 'Finished' : 'Live';
}

/** The reason an event can no longer be found, from the API's 404 body. */
export function notFoundMessage(title: string): string {
  return `We could not find that event. ${title} may have finished, or the link may be wrong.`;
}