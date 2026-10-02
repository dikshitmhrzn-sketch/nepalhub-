/** Mirrors the Search contracts exposed by the ASP.NET Core API. */

/**
 * Which part of the site a search covers. `All` is a filter value rather than a result module: no
 * result is ever tagged with it, and the API never sends it back inside a group.
 *
 * A string union rather than a numeric enum because the API writes enums by name, so a number here
 * would be a guess about a wire format that deliberately avoids numbers.
 */
export type SearchModule = 'All' | 'Students' | 'Jobs' | 'Businesses' | 'Deals' | 'Events' | 'Calculators';

/** The modules a result can actually belong to, in the order the API groups them. */
export const resultModules: readonly SearchModule[] = [
  'Students',
  'Jobs',
  'Businesses',
  'Deals',
  'Events',
  'Calculators',
] as const;

/** The tab strip, which leads with `All` because that is the state a reader starts in. */
export const searchModules: readonly SearchModule[] = ['All', ...resultModules] as const;

/**
 * The tab labels.
 *
 * These are the module's own names as a reader would say them, which is not the same as the route
 * segment or the API's enum name: the offers module is called Offers everywhere a person can see
 * it, and the calculators module is called Tools.
 */
export const moduleLabels: Record<SearchModule, string> = {
  All: 'Everything',
  Students: 'Student Hub',
  Jobs: 'Jobs',
  Businesses: 'Businesses',
  Deals: 'Offers',
  Events: 'Events',
  Calculators: 'Tools',
};

/**
 * One result, in the shape the API sends it.
 *
 * Almost everything is null, because six modules do not share a shape: an offer has dates and no
 * employer, a job has an employer and no dates. Each module fills in what it has. A result never
 * carries a link path — the server publishes the module and the slug, and `resultHref` in
 * `./format` builds the address, because the client owns its own routes.
 */
export interface SearchResult {
  module: Exclude<SearchModule, 'All'>;
  referenceId: string;
  slug: string;
  title: string;
  summary: string | null;
  category: string | null;
  organisation: string | null;
  workMode: string | null;
  provider: string | null;
  discountText: string | null;
  organiser: string | null;
  venue: string | null;
  place: string | null;
  isVerified: boolean;
  startsOn: string | null;
  endsOn: string | null;
  applicationDeadline: string | null;
  expiresOn: string | null;
  startsAt: string | null;
  endsAt: string | null;
}

/** Every hit from one module, in relevance order. */
export interface SearchGroup {
  module: Exclude<SearchModule, 'All'>;
  label: string;
  totalCount: number;
  items: SearchResult[];
}

/**
 * A page of results.
 *
 * `truncated` is the honest part of the contract and the reason the UI must not claim an exact
 * total. The API takes a bounded number of candidates from each module and counts nothing, so a
 * term matching thousands of rows reports its strongest matches rather than a number it cannot
 * support. `totalCount` is therefore "candidates ranked", not "everything that matches".
 */
export interface SearchResponse {
  query: string;
  page: number;
  pageSize: number;
  totalCount: number;
  truncated: boolean;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
  groups: SearchGroup[];
}

export interface SearchQuery {
  q: string;
  page?: number;
  pageSize?: number;
  module?: SearchModule;
}

/** Kept in step with `SearchLimits` on the server, which is what refuses a query outside them. */
export const searchLimits = {
  minQueryLength: 2,
  maxQueryLength: 100,
  defaultPageSize: 20,
  maxPageSize: 30,
} as const;

export const minQueryLength = searchLimits.minQueryLength;

/**
 * The smallest term the API will search, used to decide whether to show a hint rather than to guard
 * the request. The server refuses anything shorter, so a term below this must never be sent: the
 * page would render an error for a keystroke nobody finished typing.
 */
export function isSearchable(term: string): boolean {
  return term.trim().length >= minQueryLength;
}