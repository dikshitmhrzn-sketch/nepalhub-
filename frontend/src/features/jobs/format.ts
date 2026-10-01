import type { EmploymentType, JobCategory, JobStatus, WorkMode } from './types';
import {
  categoryLabels,
  employmentTypeLabels,
  sortLabels,
  workModeLabels,
} from './types';

/**
 * A `DateOnly` arrives as a bare `yyyy-MM-dd` with no zone, which is what makes it safe to
 * read: appending a time and letting the browser localise it is what turns a deadline into
 * yesterday for a reader west of Nepal. The parts are formatted by hand so the date on screen
 * is the date the server meant.
 */
function parseDateOnly(value: string): { year: string; month: string; day: string } {
  const [year, month, day] = value.split('-');

  return { year, month, day };
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

/** "12 March 2081" for a deadline, or the raw value when it is not a date. */
export function formatDeadline(value: string): string {
  const { year, month, day } = parseDateOnly(value);
  const monthName = monthNames[Number(month) - 1];

  return monthName ? `${Number(day)} ${monthName} ${year}` : value;
}

/** The "posted" line on a card, from the API's `DateTime`. */
export function formatPostedAt(value: string): string {
  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

/**
 * How a posting reads on a card, for example "Full time · Remote". The location is left out
 * here because a remote posting has none, and a location line is only worth printing when
 * there is something to print.
 */
export function describeJob(employmentType: EmploymentType, workMode: WorkMode): string {
  return `${employmentTypeLabels[employmentType]} · ${workModeLabels[workMode]}`;
}

/** "Front-end developer · Kathmandu", or just the province when a district is not set. */
export function formatLocation(provinceName: string | null, districtName: string | null): string | null {
  if (districtName) {
    return provinceName ? `${districtName}, ${provinceName}` : districtName;
  }

  return provinceName;
}

export function categoryLabel(category: JobCategory): string {
  return categoryLabels[category];
}

export function employmentTypeLabel(employmentType: EmploymentType): string {
  return employmentTypeLabels[employmentType];
}

export function workModeLabel(workMode: WorkMode): string {
  return workModeLabels[workMode];
}

export function sortLabel(sort: keyof typeof sortLabels): string {
  return sortLabels[sort];
}

/** The badge on a card. Only "closing soon" is worth saying out loud. */
export function statusLabel(status: JobStatus): string {
  return status === 'ClosingSoon' ? 'Closing soon' : 'Open';
}

/**
 * The staff row's status, which is a different thing from the reader's: a draft is neither
 * open nor closed, it is simply not out yet.
 */
export function describeStaffRow(row: {
  isPublished: boolean;
  isExpired: boolean;
}): string {
  if (!row.isPublished) {
    return 'Draft';
  }

  return row.isExpired ? 'Closed' : 'Published';
}

/**
 * Turns a title into the slug the API will accept, for the "generate from the title"
 * button in the editor. Anything that is not a lowercase letter or a digit becomes a
 * hyphen, and runs of hyphens collapse, so the result is checked against the same pattern
 * the server enforces rather than being close enough to it.
 */
export function slugifyTitle(title: string): string {
  return title
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 160);
}