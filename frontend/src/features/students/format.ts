import type { EducationLevel, ResourceCategory, ResourceType } from './types';
import { categoryLabels, levelLabels, typeLabels } from './types';

/**
 * A `DateOnly` arrives as a bare `yyyy-MM-dd` with no zone, which is what makes it safe
 * to read: appending a time and letting the browser localise it is what turns a deadline
 * into yesterday for a reader west of Nepal. The parts are formatted by hand so the date
 * on screen is the date the server meant.
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

/** "12 March 2081" for a deadline, or null when the row has no date. */
export function formatDeadline(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const { year, month, day } = parseDateOnly(value);
  const monthName = monthNames[Number(month) - 1];

  return monthName ? `${Number(day)} ${monthName} ${year}` : value;
}

/** The "updated" line on a card, from the API's `DateTime`. */
export function formatUpdatedAt(value: string): string {
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

const nprFormatter = new Intl.NumberFormat('en-NP', {
  maximumFractionDigits: 0,
  minimumFractionDigits: 0,
});

/** "NPR 45,000", or null when the row has no amount. */
export function formatAward(value: number | null): string | null {
  return value === null ? null : `NPR ${nprFormatter.format(value)}`;
}

export function typeLabel(type: ResourceType): string {
  return typeLabels[type];
}

export function categoryLabel(category: ResourceCategory): string {
  return categoryLabels[category];
}

export function levelLabel(level: EducationLevel): string {
  return levelLabels[level];
}

/** The short badge line on a card, for example "Question paper · Science". */
export function describeResource(type: ResourceType, category: ResourceCategory): string {
  return `${typeLabels[type]} · ${categoryLabels[category]}`;
}
