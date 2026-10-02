import { categoryLabels as businessCategoryLabels } from '@/features/businesses/types';
import { workModeLabels } from '@/features/jobs/types';
import { typeLabels as resourceTypeLabels } from '@/features/students/types';

import type { SearchModule, SearchResult } from './types';

/**
 * Where a result lives, built here rather than sent by the API.
 *
 * The server publishes the module and the slug and stops there. A path it chose would be a claim
 * about this client's routes, and it would break the first time a route moved — or the first time a
 * second client, such as the planned mobile app, wanted the same results for a different set of
 * paths. One table, in the one place that owns them.
 *
 * Null comes back for a module this build does not know about. The API is free to add one before
 * this client ships, and a result it sent would otherwise be rendered as a link to `undefined`, which
 * is worse than showing it as plain text.
 */
const modulePaths: Partial<Record<SearchModule, string>> = {
  Students: '/students/resources',
  Jobs: '/jobs',
  Businesses: '/businesses',
  Deals: '/deals',
  Events: '/events',
  // The calculators are listed at /tools but their detail pages live at /calculators/:slug, so this
  // is not the address the tools page is on.
  Calculators: '/calculators',
};

/**
 * The slug is percent-encoded because a slug is not guaranteed to be safe inside a path, and an
 * unencoded one would let a hostile row send the reader somewhere else.
 */
export function resultHref(result: Pick<SearchResult, 'module' | 'slug'>): string | null {
  const path = modulePaths[result.module];

  return path ? `${path}/${encodeURIComponent(result.slug)}` : null;
}

/**
 * The one-line description under a result's title, built from whichever fields that module fills.
 *
 * One function rather than six card components, because the point of the grouped response is that a
 * reader scans one list: a different card per module would make the page harder to read, not easier,
 * and would defeat the grouping.
 */
export function resultMeta(result: SearchResult): string | null {
  switch (result.module) {
    case 'Students':
      // A resource's `category` slot carries its type — the API sends `QuestionPaper`, not a
      // subject — so it is read through the type labels rather than the category ones.
      return joinParts([
        label(resourceTypeLabels, result.category),
        result.provider,
        result.expiresOn ? `closes ${formatDay(result.expiresOn)}` : null,
      ]);

    case 'Jobs':
      return joinParts([
        result.organisation,
        label(workModeLabels, result.workMode),
        result.place,
        result.applicationDeadline
          ? `apply by ${formatDay(result.applicationDeadline)}`
          : null,
      ]);

    case 'Businesses':
      return joinParts([
        label(businessCategoryLabels, result.category),
        result.place,
        result.isVerified ? 'Verified' : null,
      ]);

    case 'Deals':
      return joinParts([
        result.discountText,
        result.place,
        result.endsOn ? `until ${formatDay(result.endsOn)}` : null,
      ]);

    case 'Events':
      return joinParts([
        result.organiser,
        result.venue ?? result.place,
        result.startsAt ? `${formatDay(result.startsAt)}, ${formatWallClockTime(result.startsAt)}` : null,
      ]);

    case 'Calculators':
      return result.category;
  }
}

/**
 * The count line above the results.
 *
 * When the API has capped the search this says "of the strongest matches" rather than claiming a
 * total. Each module contributes a bounded number of candidates and nothing is counted, so the
 * honest statement is how many were ranked, not how many exist.
 */
export function countLabel(totalCount: number, truncated: boolean): string {
  const noun = totalCount === 1 ? 'result' : 'results';

  return truncated ? `${totalCount} strongest ${noun}` : `${totalCount} ${noun}`;
}

/**
 * "12 March" from a bare `yyyy-MM-dd`, with the year added only when it is not the current one.
 *
 * The parts are formatted by hand rather than through `Date`, for the reason every module in this
 * app formats dates the same way: letting the browser localise a date-only value turns an end date
 * into yesterday for a reader west of Nepal. Only the first ten characters are read, so a value that
 * arrives with a time attached still renders as a date rather than as `NaN October`.
 */
function formatDay(value: string): string {
  const [year, month, day] = value.slice(0, 10).split('-');
  const monthName = monthNames[Number(month) - 1];

  if (!year || !day || !monthName) {
    return value;
  }

  return new Date().getFullYear() === Number(year)
    ? `${Number(day)} ${monthName}`
    : `${Number(day)} ${monthName} ${year}`;
}

/**
 * "3pm" or "3:30pm" from a stored event time.
 *
 * The value is Nepal wall-clock time with no offset attached, exactly as the API stores it. It is
 * read as text and never turned into an instant, because turning it into one would re-introduce the
 * time-zone bug the wall-clock format exists to avoid.
 */
function formatWallClockTime(value: string): string {
  const match = /\d{2}-\d{2}[T ](\d{2}):(\d{2})/.exec(value);

  if (!match) {
    return '';
  }

  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  const meridiem = hours < 12 ? 'am' : 'pm';
  const hour = hours % 12 === 0 ? 12 : hours % 12;

  return `${hour}${minutes === 0 ? '' : `:${`${minutes}`.padStart(2, '0')}`}${meridiem}`;
}

/**
 * Turns an enum name the API sent into the words a reader should see.
 *
 * The maps are the same ones each module's own card uses, imported rather than copied. Two copies of
 * one label table would be free today and wrong eventually: a module that renames a category would
 * leave the search results showing the old word on the same row that its own page now spells
 * differently.
 *
 * The value falls back to itself when there is no entry. The API is written by name and can gain a
 * category before this client knows about it, and printing `HealthcareServices` is a rough edge rather
 * than a failure — the search result is still there and still correct.
 */
function label(labels: Record<string, string>, value: string | null): string | null {
  return value ? (labels[value] ?? value) : null;
}

function joinParts(parts: (string | null | undefined)[]): string | null {
  const kept = parts.filter((part): part is string => Boolean(part && part.trim()));

  return kept.length > 0 ? kept.join(' · ') : null;
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