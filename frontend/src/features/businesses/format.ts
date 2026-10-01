import type {
  BusinessCategory,
  BusinessSort,
  BusinessSummary,
  DayOfWeek,
  OpenNow,
} from './types';
import { categoryLabels, shortDayNames, sortLabels } from './types';

/**
 * A Nepal phone number is stored as digits only, so one business cannot appear under two
 * spellings of the same number. A reader, though, expects a number they can read at a glance.
 * This groups the common forms and leaves anything unexpected as the raw digits rather than
 * guessing at a layout that does not exist.
 */
export function formatPhone(value: string): string {
  if (value.length === 10) {
    return `${value.slice(0, 3)}-${value.slice(3, 6)}-${value.slice(6)}`;
  }

  if (value.length === 9 && value.startsWith('0')) {
    return `${value.slice(0, 1)}-${value.slice(1, 4)}-${value.slice(4)}`;
  }

  if (value.length === 7) {
    return `${value.slice(0, 3)}-${value.slice(3)}`;
  }

  return value;
}

/**
 * How a card reads its location. Both parts are always present for a business, because a district
 * is required, so there is no "province only" case the way there is on a remote job.
 */
export function formatLocation(provinceName: string, districtName: string): string {
  return districtName ? `${districtName}, ${provinceName}` : provinceName;
}

/**
 * The open/closed line on a card. Null means the listing published no hours at all, which is a
 * different statement from "closed", so it says so rather than showing a red badge.
 */
export function openNowLabel(isOpenNow: OpenNow): string {
  if (isOpenNow === null) {
    return 'Hours not listed';
  }

  return isOpenNow ? 'Open now' : 'Closed now';
}

/** The same state as a class suffix, for colouring the badge. */
export function openNowTone(isOpenNow: OpenNow): 'open' | 'closed' | 'unknown' {
  if (isOpenNow === null) {
    return 'unknown';
  }

  return isOpenNow ? 'open' : 'closed';
}

/**
 * Times arrive as `HH:mm:ss`. A reader wants `09:00`, and a closing time past midnight is shown
 * with a marker so "22:00 to 02:00" does not look like a typo.
 */
export function formatTime(value: string): string {
  const [hours, minutes] = value.split(':');

  return `${hours}:${minutes}`;
}

/**
 * One day's hours as a single line: "09:00 to 18:00", or "Closed". A closing time earlier than
 * the opening time is a shift that runs past midnight, so it is marked rather than silently
 * reversed.
 */
export function describeHours(opensAt: string | null, closesAt: string | null, isClosed: boolean): string {
  if (isClosed || !opensAt || !closesAt) {
    return 'Closed';
  }

  const overnight = closesAt < opensAt;

  return overnight
    ? `${formatTime(opensAt)} to ${formatTime(closesAt)} (next day)`
    : `${formatTime(opensAt)} to ${formatTime(closesAt)}`;
}

export function dayLabel(day: DayOfWeek): string {
  return shortDayNames[day];
}

export function categoryLabel(category: BusinessCategory): string {
  return categoryLabels[category];
}

export function sortLabel(sort: BusinessSort): string {
  return sortLabels[sort];
}

/**
 * The keyword a search box is filled with when a category chip is opened, so the URL is
 * shareable without inventing a second query parameter the API does not know about.
 */
export function describeBusiness(business: Pick<BusinessSummary, 'category' | 'districtName'>): string {
  return `${categoryLabels[business.category]} · ${business.districtName}`;
}

/**
 * Turns a business name into the slug the API will accept, for the "generate from the name"
 * button in the editor. Anything that is not a lowercase letter or a digit becomes a hyphen, and
 * runs of hyphens collapse, so the result is checked against the same pattern the server enforces
 * rather than being close to it.
 */
export function slugifyName(name: string): string {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 160);
}

/**
 * A time posted by an `<input type="time">` is `HH:mm`, which is what the API accepts for a
 * `TimeOnly`. The seconds the server returns are dropped so the two round-trip through the same
 * control without the editor comparing "09:00" against "09:00:00" and deciding the field changed.
 */
export function toInputTime(value: string | null): string {
  if (!value) {
    return '';
  }

  const [hours, minutes] = value.split(':');

  return hours && minutes ? `${hours}:${minutes}` : value;
}

/** The API accepts `HH:mm` or `HH:mm:ss`; the editor sends the latter so the wire shape is stable. */
export function fromInputTime(value: string): string | null {
  return value ? `${value}:00` : null;
}