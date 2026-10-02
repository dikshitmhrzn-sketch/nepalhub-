/** Mirrors the Events contracts exposed by the ASP.NET Core API. */

/** The API serialises enums by name, so these are the literal wire values. */
export type EventCategory =
  | 'Community'
  | 'Cultural'
  | 'Sports'
  | 'Education'
  | 'Religious'
  | 'Business'
  | 'Exhibition'
  | 'Other';

/** How the public calendar is ordered. */
export type EventSort = 'Soonest' | 'Newest';

/** Which slice of the calendar the staff screen is showing. */
export type EventAdminFilter = 'All' | 'Published' | 'Draft';

/** The one shape every paged endpoint in the API returns. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/**
 * One row on an event card. Note what is missing: the description. A card is what, where, when and
 * who is running it, and a page of cards is read by scanning rather than reading.
 *
 * `isOngoing` is derived from the current Nepal time on every read and stored nowhere: "happening
 * now" stops being true at the finishing time on its own, and a stored flag would be wrong by the
 * evening.
 */
export interface EventSummary {
  id: string;
  slug: string;
  title: string;
  summary: string | null;
  category: EventCategory;
  organiser: string | null;
  businessName: string | null;
  districtName: string;
  provinceName: string;
  venue: string | null;
  startsAt: string;
  endsAt: string;
  isOngoing: boolean;
  createdAt: string;
}

/** One event in full, for a reader who has opened it. */
export interface EventDetail extends Omit<EventSummary, 'createdAt'> {
  description: string;
  businessSlug: string | null;
  registrationUrl: string | null;
  updatedAt: string;
}

/**
 * The staff list row. Drafts and finished events are both here, which is the entire difference from
 * the public list. A finished event stays in the staff board rather than disappearing, because the
 * question a staff member brings to this screen is usually "why is my event not showing", and the
 * answer is often that it ended.
 */
export interface EventAdminSummary {
  id: string;
  slug: string;
  title: string;
  category: EventCategory;
  districtName: string;
  isPublished: boolean;
  isFinished: boolean;
  startsAt: string;
  updatedAt: string;
  createdByEmail: string | null;
}

/**
 * The staff view of one row, which also carries the reasons a draft cannot be published yet.
 * `publishBlockers` comes from the same validation the publish path enforces, so the form and the
 * server cannot drift apart.
 */
export interface EventAdmin
  extends Omit<EventAdminSummary, 'isFinished' | 'createdByEmail' | 'updatedAt' | 'districtName'> {
  summary: string | null;
  description: string;
  organiser: string | null;
  businessSlug: string | null;
  districtCode: string | null;
  districtName: string | null;
  provinceCode: string | null;
  provinceName: string | null;
  venue: string | null;
  endsAt: string;
  registrationUrl: string | null;
  isFinished: boolean;
  createdAt: string;
  updatedAt: string;
  createdByEmail: string | null;
  publishBlockers: string[];
}

/** A business an event can be attached to, for the staff editor's picker. */
export interface EventBusinessOption {
  slug: string;
  name: string;
}

/** Create and update share one body, so the staff form sends the same shape for both. */
export interface SaveEventRequest {
  slug: string;
  title: string;
  summary: string | null;
  description: string;
  category: EventCategory;
  organiser: string | null;
  businessSlug: string | null;
  districtCode: string | null;
  venue: string | null;
  registrationUrl: string | null;
  startsAt: string;
  endsAt: string;
  isPublished: boolean;
}

export interface EventListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  province?: string;
  district?: string;
  category?: EventCategory;
  sort?: EventSort;
}

export interface EventAdminListQuery extends EventListQuery {
  status?: EventAdminFilter;
}

export interface FieldError {
  field: string;
  code: string;
  message: string;
}

export const eventCategories: readonly EventCategory[] = [
  'Community',
  'Cultural',
  'Sports',
  'Education',
  'Religious',
  'Business',
  'Exhibition',
  'Other',
];

export const eventSorts: readonly EventSort[] = ['Soonest', 'Newest'];

export const eventAdminFilters: readonly EventAdminFilter[] = ['All', 'Published', 'Draft'];

/**
 * The wire values are enum names, which are not what a reader should be shown. These labels are what
 * appear on screen, in a select or a badge.
 */
export const categoryLabels: Record<EventCategory, string> = {
  Community: 'Community',
  Cultural: 'Cultural',
  Sports: 'Sports',
  Education: 'Education',
  Religious: 'Religious',
  Business: 'Business',
  Exhibition: 'Exhibition',
  Other: 'Other',
};

export const sortLabels: Record<EventSort, string> = {
  Soonest: 'Starting soonest',
  Newest: 'Newest first',
};

export const adminFilterLabels: Record<EventAdminFilter, string> = {
  All: 'All events',
  Published: 'Published',
  Draft: 'Drafts',
};

/**
 * A blank row for the editor.
 *
 * The category defaults to its first member rather than to nothing, because the API binds an enum
 * and a select cannot present "unset". Everything else starts empty, which is a legitimate draft:
 * the editor is told what publishing needs rather than being blocked from saving.
 *
 * The start is prefilled to a plausible future hour in Nepal time rather than to the current minute,
 * so the first save is not refused for an event that has already started. A staff member is expected
 * to set both times properly.
 */
export function emptyEventRequest(): SaveEventRequest {
  return {
    slug: '',
    title: '',
    summary: null,
    description: '',
    category: 'Community',
    organiser: null,
    businessSlug: null,
    districtCode: null,
    venue: null,
    registrationUrl: null,
    startsAt: defaultStart(),
    endsAt: defaultEnd(),
    isPublished: false,
  };
}

/**
 * Nepal Standard Time as a browser can compute it without a time-zone database: UTC+05:45. The
 * minutes are read off the machine clock and the offset subtracted, because the two together give
 * the Nepal wall clock whether or not the browser's own zone is Nepal.
 */
function nepalNow(): Date {
  const now = new Date();

  return new Date(now.getTime() + (5 * 60 + 45 + now.getTimezoneOffset()) * 60 * 1000);
}

/**
 * Tomorrow at ten in the morning. Deliberately in the future rather than "now": the API refuses to
 * publish an event that has already ended, and an event prefilled to the current minute would be
 * over within minutes of the page being opened.
 */
function defaultStart(): string {
  const start = nepalNow();
  start.setDate(start.getDate() + 1);
  start.setHours(10, 0, 0, 0);

  return toLocalInputValue(start);
}

/** Two hours after the start, which is a plausible length and always a valid one. */
function defaultEnd(): string {
  const end = nepalNow();
  end.setDate(end.getDate() + 1);
  end.setHours(12, 0, 0, 0);

  return toLocalInputValue(end);
}

/**
 * The `yyyy-MM-ddTHH:mm` an `<input type="datetime-local">` uses. It has no zone by design: the API
 * reads these as Nepal local wall-clock times, which is the only reading that means anything for a
 * Nepali event, and appending a `Z` here would shift every event by five and three-quarter hours.
 */
export function toLocalInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  const hours = `${date.getHours()}`.padStart(2, '0');
  const minutes = `${date.getMinutes()}`.padStart(2, '0');

  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

/**
 * Reads a `DateTime` from the API as Nepal wall-clock parts.
 *
 * The API stores these as bare Nepal local times with no zone, so they arrive as
 * `2026-03-14T10:00:00`. Handing that to `new Date()` would be read as the browser's own local
 * time, which moves the event by a different number of hours depending on where the reader is. The
 * parts are therefore taken as text and formatted by hand, so a reader in Chicago and a reader in
 * Kathmandu are shown the same ten o'clock.
 */
export function parseNepalWallClock(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::(\d{2}))?/.exec(value);

  if (!match) {
    return null;
  }

  return new Date(
    Number(match[1]),
    Number(match[2]) - 1,
    Number(match[3]),
    Number(match[4]),
    Number(match[5]),
    Number(match[6] ?? 0),
  );
}