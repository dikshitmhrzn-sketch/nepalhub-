import type { ReportStatus } from './types';

/** The status as the queue says it, for the badge on a row. */
export function statusLabel(status: ReportStatus): string {
  switch (status) {
    case 'Open':
      return 'Open';
    case 'Resolved':
      return 'Resolved';
    default:
      return 'Dismissed';
  }
}

/**
 * When a member filed the complaint, in the reader's own time zone. The queue answers
 * with a timestamp, and a moderator working the list needs "when" rather than a raw
 * instant: every other date on the site is read the same way.
 */
export function formatFiledAt(value: string): string {
  const parsed = new Date(value);

  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      });
}
