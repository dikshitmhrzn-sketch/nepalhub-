/** Mirrors the Deals contracts exposed by the ASP.NET Core API. */

/** How the public offers page is ordered. */
export type DealSort = 'EndingSoon' | 'Newest';

/** Which slice of the offers list the staff screen is showing. A reader has no equivalent of Draft. */
export type DealAdminFilter = 'All' | 'Published' | 'Draft';

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
 * One row on an offer card. Note what is missing: the description. A card is the offer, the place
 * and how long it has left, and carrying the full terms on every row of a page would make the list
 * several times larger for content the reader has not asked for.
 *
 * `districtName` is a string here but is empty for a nationwide offer, because there is no district
 * to name. The card renders that as "Nationwide" rather than leaving a blank the reader would have
 * to interpret, which is why the emptiness is handled here rather than in the API.
 */
export interface DealSummary {
  id: string;
  slug: string;
  title: string;
  summary: string | null;
  discountText: string | null;
  businessName: string | null;
  businessSlug: string | null;
  districtName: string;
  provinceName: string;
  startsOn: string;
  endsOn: string;
  daysRemaining: number;
  createdAt: string;
}

/**
 * One offer in full, for a reader who has opened it. `isRunning` is computed against the current
 * Nepal date on every read and stored nowhere, so an offer that starts tomorrow is never described
 * as running today.
 */
export interface DealDetail
  extends Omit<DealSummary, 'createdAt' | 'districtName' | 'provinceName' | 'daysRemaining'> {
  description: string;
  districtName: string | null;
  provinceName: string | null;
  redemptionUrl: string | null;
  daysRemaining: number;
  isRunning: boolean;
  updatedAt: string;
}

/**
 * The staff list row. Drafts and expired offers are both here, which is the entire difference from
 * the public list. An expired offer stays in the staff board rather than vanishing, because the
 * question a staff member brings to this screen is usually "why is this not showing".
 */
export interface DealAdminSummary {
  id: string;
  slug: string;
  title: string;
  districtName: string | null;
  businessName: string | null;
  isPublished: boolean;
  isExpired: boolean;
  endsOn: string;
  updatedAt: string;
  createdByEmail: string | null;
}

/**
 * The staff view of one row, which also carries the reasons a draft cannot be published yet.
 * `publishBlockers` comes from the same validation the publish path enforces, so the form can say
 * what is missing instead of leaving staff to find out on save.
 */
export interface DealAdmin
  extends Omit<
    DealAdminSummary,
    'isExpired' | 'createdByEmail' | 'districtName' | 'endsOn' | 'updatedAt'
  > {
  summary: string | null;
  description: string;
  discountText: string | null;
  businessSlug: string | null;
  districtCode: string | null;
  districtName: string | null;
  provinceCode: string | null;
  provinceName: string | null;
  redemptionUrl: string | null;
  startsOn: string;
  endsOn: string;
  isExpired: boolean;
  createdAt: string;
  updatedAt: string;
  createdByEmail: string | null;
  publishBlockers: string[];
}

/** A business an offer can be attached to, for the staff editor's picker. */
export interface DealBusinessOption {
  slug: string;
  name: string;
}

/** Create and update share one body, so the staff form sends the same shape for both. */
export interface SaveDealRequest {
  slug: string;
  title: string;
  summary: string | null;
  description: string;
  discountText: string | null;
  businessSlug: string | null;
  districtCode: string | null;
  redemptionUrl: string | null;
  startsOn: string;
  endsOn: string;
  isPublished: boolean;
}

export interface DealListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  province?: string;
  district?: string;
  sort?: DealSort;
}

export interface DealAdminListQuery extends DealListQuery {
  status?: DealAdminFilter;
}

export interface FieldError {
  field: string;
  code: string;
  message: string;
}

export const dealSorts: readonly DealSort[] = ['EndingSoon', 'Newest'];

export const dealAdminFilters: readonly DealAdminFilter[] = ['All', 'Published', 'Draft'];

export const sortLabels: Record<DealSort, string> = {
  EndingSoon: 'Ending soonest',
  Newest: 'Newest first',
};

export const adminFilterLabels: Record<DealAdminFilter, string> = {
  All: 'All offers',
  Published: 'Published',
  Draft: 'Drafts',
};

/**
 * A blank row for the editor.
 *
 * The dates start today and run for a fortnight, because an offer with no end is an offer that
 * never leaves the page and this module exists to retire offers. A staff member is expected to
 * change both; starting them in the past would let the publish rule refuse the very first save.
 */
export function emptyDealRequest(): SaveDealRequest {
  return {
    slug: '',
    title: '',
    summary: null,
    description: '',
    discountText: null,
    businessSlug: null,
    districtCode: null,
    redemptionUrl: null,
    startsOn: toDateInput(addDays(todayInNepal(), 1)),
    endsOn: toDateInput(addDays(todayInNepal(), 14)),
    isPublished: false,
  };
}

/**
 * The Nepal calendar date, read from the browser clock shifted into Nepal Standard Time. Used only
 * to prefill a blank editor form; every date the API returns is used as given, and every date the
 * editor posts is a bare `yyyy-MM-dd` with no zone, which is the point.
 *
 * The shift rather than `toLocaleDateString` with a time zone is deliberate: the result has to be
 * the same string the server would compute, whatever locale the browser is set to.
 */
function todayInNepal(): Date {
  const now = new Date();

  return new Date(now.getTime() + (5 * 60 + 45 + now.getTimezoneOffset()) * 60 * 1000);
}

function addDays(date: Date, days: number): Date {
  const shifted = new Date(date.getTime());

  shifted.setDate(shifted.getDate() + days);

  return shifted;
}

/** The `yyyy-MM-dd` an `<input type="date">` and the API both use. */
export function toDateInput(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');

  return `${year}-${month}-${day}`;
}