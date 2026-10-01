/** Mirrors the Businesses contracts exposed by the ASP.NET Core API. */

/** The API serialises enums by name, so these are the literal wire values. */
export type BusinessCategory =
  | 'Restaurants'
  | 'Cafes'
  | 'HotelsAndAccommodation'
  | 'GymsAndFitness'
  | 'SalonsAndBeauty'
  | 'EducationAndTuition'
  | 'RepairAndMaintenance'
  | 'Shops'
  | 'InformationTechnology'
  | 'ProfessionalServices'
  | 'HealthcareServices'
  | 'Other';

/** Which slice of the directory the staff list shows. A reader has no equivalent of Draft. */
export type BusinessAdminFilter = 'All' | 'Published' | 'Draft';

/** How the public directory is ordered. */
export type BusinessSort = 'Name' | 'Newest';

/** The seven days, in the order the API returns them: Sunday first. */
export type DayOfWeek = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

/**
 * "Open now", or null when the listing published no hours at all. Null is not the same as false:
 * one means unknown and the other is a fact, and showing a closed badge for a business that
 * simply never listed its hours would be telling the reader something untrue.
 */
export type OpenNow = boolean | null;

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
 * One row on a business card. Note what is missing: the description. Carrying it on every row of
 * a directory would make the list several times larger for content the reader has not asked for,
 * and the list endpoint stays a single SQL projection as a result.
 */
export interface BusinessSummary {
  id: string;
  slug: string;
  name: string;
  category: BusinessCategory;
  districtName: string;
  provinceName: string;
  address: string | null;
  phone: string | null;
  isVerified: boolean;
  isOpenNow: OpenNow;
  createdAt: string;
}

/** One weekday's hours. A closed day carries no times. */
export interface BusinessHours {
  dayOfWeek: DayOfWeek;
  isClosed: boolean;
  opensAt: string | null;
  closesAt: string | null;
}

/** One business in full, for a reader who has opened it. */
export interface BusinessDetail extends Omit<BusinessSummary, 'createdAt'> {
  description: string;
  email: string | null;
  website: string | null;
  openingHours: BusinessHours[];
  updatedAt: string;
}

/** The staff list row, which shows drafts as well as live listings. */
export interface BusinessAdminSummary {
  id: string;
  slug: string;
  name: string;
  category: BusinessCategory;
  districtName: string;
  isPublished: boolean;
  isVerified: boolean;
  updatedAt: string;
  createdByEmail: string | null;
}

/**
 * The staff view of one row. `districtCode` is what the editor posts back, and `publishBlockers`
 * comes from the same validation the publish path uses, so the form can say what is missing
 * instead of leaving staff to find out on save.
 */
export interface BusinessAdmin extends Omit<BusinessAdminSummary, 'createdByEmail'> {
  description: string;
  districtCode: string;
  provinceCode: string;
  provinceName: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  verifiedAt: string | null;
  openingHours: BusinessHours[];
  createdAt: string;
  createdByEmail: string | null;
  publishBlockers: string[];
}

/** One weekday of hours as the staff form sends it. */
export interface SaveBusinessHours {
  dayOfWeek: DayOfWeek;
  isClosed: boolean;
  opensAt: string | null;
  closesAt: string | null;
}

/** Create and update share one body, so the staff form sends the same shape for both. */
export interface SaveBusinessRequest {
  slug: string;
  name: string;
  description: string;
  category: BusinessCategory;
  districtCode: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  isPublished: boolean;
  isVerified: boolean;
  openingHours: SaveBusinessHours[];
}

export interface BusinessListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  province?: string;
  district?: string;
  category?: BusinessCategory;
  verified?: boolean;
  sort?: BusinessSort;
}

export interface BusinessAdminListQuery extends Omit<BusinessListQuery, 'sort'> {
  status?: BusinessAdminFilter;
}

export interface FieldError {
  field: string;
  code: string;
  message: string;
}

export const businessCategories: readonly BusinessCategory[] = [
  'Restaurants',
  'Cafes',
  'HotelsAndAccommodation',
  'GymsAndFitness',
  'SalonsAndBeauty',
  'EducationAndTuition',
  'RepairAndMaintenance',
  'Shops',
  'InformationTechnology',
  'ProfessionalServices',
  'HealthcareServices',
  'Other',
];

export const businessSorts: readonly BusinessSort[] = ['Name', 'Newest'];

export const businessAdminFilters: readonly BusinessAdminFilter[] = ['All', 'Published', 'Draft'];

/**
 * Sunday first, because that is the order the API returns the week in and because it matches the
 * Nepali week as most calendars here print it. Reordering here would make the editor disagree
 * with the detail page for no gain.
 */
export const daysOfWeek: readonly DayOfWeek[] = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
];

/** Short forms for the hours table, where the full name would push the columns too wide. */
export const shortDayNames: Record<DayOfWeek, string> = {
  Sunday: 'Sun',
  Monday: 'Mon',
  Tuesday: 'Tue',
  Wednesday: 'Wed',
  Thursday: 'Thu',
  Friday: 'Fri',
  Saturday: 'Sat',
};

/**
 * The wire values are enum names, which are not what a reader should be shown. These labels are
 * what appear on screen, in a select or a badge.
 */
export const categoryLabels: Record<BusinessCategory, string> = {
  Restaurants: 'Restaurants',
  Cafes: 'Cafes',
  HotelsAndAccommodation: 'Hotels and accommodation',
  GymsAndFitness: 'Gyms and fitness',
  SalonsAndBeauty: 'Salons and beauty',
  EducationAndTuition: 'Education and tuition',
  RepairAndMaintenance: 'Repair and maintenance',
  Shops: 'Shops',
  InformationTechnology: 'Information technology',
  ProfessionalServices: 'Professional services',
  HealthcareServices: 'Healthcare',
  Other: 'Other',
};

export const sortLabels: Record<BusinessSort, string> = {
  Name: 'Name (A to Z)',
  Newest: 'Newest first',
};

export const adminFilterLabels: Record<BusinessAdminFilter, string> = {
  All: 'All listings',
  Published: 'Published',
  Draft: 'Drafts',
};

/**
 * A blank row for the editor. The category defaults to its first member rather than to nothing,
 * because the API binds an enum and a select cannot present "unset". Every other field is empty on
 * purpose: a draft with a name and a district is a legitimate thing to save, and the editor is
 * told the rest of what publishing needs rather than being blocked from saving.
 */
export function emptyBusinessRequest(): SaveBusinessRequest {
  return {
    slug: '',
    name: '',
    description: '',
    category: 'Restaurants',
    districtCode: null,
    address: null,
    phone: null,
    email: null,
    website: null,
    isPublished: false,
    isVerified: false,
    openingHours: [],
  };
}

/**
 * A blank week. Every day is open until a staff member closes one, because a shop that publishes
 * hours normally has most of them; the alternative, starting from a closed week, would mean
 * turning seven days on by hand every time a listing is created.
 */
export function emptyWeek(): SaveBusinessHours[] {
  return daysOfWeek.map((dayOfWeek) => ({
    dayOfWeek,
    isClosed: false,
    opensAt: '09:00:00',
    closesAt: '18:00:00',
  }));
}