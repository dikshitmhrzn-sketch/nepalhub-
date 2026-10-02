/**
 * A `DateOnly` arrives as a bare `yyyy-MM-dd` with no zone, which is what makes it safe to read:
 * appending a time and letting the browser localise it is what turns an end date into yesterday
 * for a reader west of Nepal. The parts are formatted by hand so the date on screen is the date
 * the server meant.
 *
 * Only the first ten characters are read, so a value that arrives with a time attached renders its
 * date rather than "NaN October" — a broken date is worse than a slightly imprecise one, and the
 * parts are discarded either way.
 */
function parseDateOnly(value: string): { year: string; month: string; day: string } | null {
  const [year, month, day] = value.slice(0, 10).split('-');

  if (!year || !month || !day) {
    return null;
  }

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

/** "12 March" or "12 March 2081", the year added only when it is not the current one. */
export function formatNepalDate(value: string): string {
  const parsed = parseDateOnly(value);

  if (!parsed) {
    return value;
  }

  const monthName = monthNames[Number(parsed.month) - 1];

  if (!monthName) {
    return value;
  }

  const day = Number(parsed.day);

  return new Date().getFullYear() === Number(parsed.year)
    ? `${day} ${monthName}`
    : `${day} ${monthName} ${parsed.year}`;
}

/**
 * How long an offer has left, in the words a reader acts on.
 *
 * This is the card's line, and it deliberately says nothing about whether the offer has started.
 * The summary endpoint does not send `isRunning`, so saying "runs from" here would mean comparing
 * `startsOn` against the browser clock in the reader's own time zone — which is the one thing this
 * module exists to stop doing. The card states what the server already stated; the detail page,
 * which is sent `isRunning`, is where the full window appears.
 */
export function remainingLabel(daysRemaining: number): string {
  if (daysRemaining <= 0) {
    return 'Ends today';
  }

  if (daysRemaining === 1) {
    return 'Ends tomorrow';
  }

  return `${daysRemaining} days left`;
}

/**
 * The date line on the detail page, where the API does send `isRunning`. An offer that has not
 * started yet says when it starts, because "5 days left" on an offer that does not run for another
 * week would otherwise read as if it could be redeemed today.
 */
export function describeRun(
  startsOn: string,
  endsOn: string,
  daysRemaining: number,
  isRunning: boolean,
): string {
  if (!isRunning) {
    return `Runs from ${formatNepalDate(startsOn)} to ${formatNepalDate(endsOn)}`;
  }

  return `${remainingLabel(daysRemaining)} · Until ${formatNepalDate(endsOn)}`;
}

/**
 * Where an offer applies. A nationwide offer has no district, and saying so is more use to a
 * reader than an empty place where the location normally goes.
 */
export function formatLocation(
  districtName: string | null,
  provinceName: string | null,
): string | null {
  if (!districtName) {
    return 'Nationwide';
  }

  return provinceName ? `${districtName}, ${provinceName}` : districtName;
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
 * The staff row's status. Three facts rather than two, because an expired offer is the one case a
 * staff member is usually looking for and "published" alone would hide it: readers cannot see it,
 * but it is not a draft either.
 */
export function describeStaffRow(row: {
  isPublished: boolean;
  isExpired: boolean;
}): 'Live' | 'Expired' | 'Draft' {
  if (!row.isPublished) {
    return 'Draft';
  }

  return row.isExpired ? 'Expired' : 'Live';
}