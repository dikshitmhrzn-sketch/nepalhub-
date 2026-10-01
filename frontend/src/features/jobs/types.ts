/** Mirrors the Jobs contracts exposed by the ASP.NET Core API. */

/** The API serialises enums by name, so these are the literal wire values. */
export type JobCategory =
  | 'InformationTechnology'
  | 'Teaching'
  | 'Healthcare'
  | 'Administration'
  | 'Finance'
  | 'Sales'
  | 'Engineering'
  | 'Construction'
  | 'Hospitality'
  | 'Agriculture'
  | 'Driving'
  | 'Security'
  | 'Government'
  | 'NGO'
  | 'Other';

export type EmploymentType = 'FullTime' | 'PartTime' | 'Contract' | 'Internship';

export type WorkMode = 'OnSite' | 'Remote' | 'Hybrid';

/**
 * Derived by the API from the deadline and the current Nepal date. There is no stored expiry
 * flag, so a row that was Open can come back as ClosingSoon on its own.
 */
export type JobStatus = 'Open' | 'ClosingSoon';

/** Which slice of the board the staff list shows. A reader has no equivalent of Expired. */
export type JobAdminFilter = 'All' | 'Published' | 'Draft' | 'Expired';

/** How the public board is ordered. */
export type JobSort = 'Newest' | 'Deadline';

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
 * One row on a job card. Note what is missing: the description. Carrying the posting text on every
 * row would make the list several times larger for content nobody has asked for yet, and the list
 * endpoint stays a single SQL projection as a result.
 */
export interface JobSummary {
  id: string;
  slug: string;
  title: string;
  organization: string;
  category: JobCategory;
  employmentType: EmploymentType;
  workMode: WorkMode;
  provinceName: string | null;
  districtName: string | null;
  applicationDeadline: string;
  status: JobStatus;
  createdAt: string;
}

/** One posting in full, for a reader who has opened it. */
export interface JobDetail extends Omit<JobSummary, 'createdAt'> {
  description: string;
  applicationUrl: string;
  updatedAt: string;
}

/** The staff list row, which shows drafts and closed rows too. */
export interface JobAdminSummary {
  id: string;
  slug: string;
  title: string;
  organization: string;
  category: JobCategory;
  employmentType: EmploymentType;
  workMode: WorkMode;
  districtName: string | null;
  applicationDeadline: string;
  isPublished: boolean;
  isExpired: boolean;
  updatedAt: string;
  createdByEmail: string | null;
}

/**
 * The staff view of one row. `districtCode` is what the editor posts back, and `publishBlockers`
 * comes from the same validation the publish path uses, so the form can say what is missing
 * instead of leaving staff to find out on save.
 */
export interface JobAdmin extends Omit<JobAdminSummary, 'createdByEmail'> {
  description: string;
  districtCode: string | null;
  provinceCode: string | null;
  provinceName: string | null;
  applicationUrl: string;
  createdAt: string;
  createdByEmail: string | null;
  publishBlockers: string[];
}

/** Create and update share one body, so the staff form sends the same shape for both. */
export interface SaveJobRequest {
  slug: string;
  title: string;
  organization: string;
  description: string;
  category: JobCategory;
  employmentType: EmploymentType;
  workMode: WorkMode;
  districtCode: string | null;
  applicationUrl: string;
  applicationDeadline: string;
  isPublished: boolean;
}

export interface JobListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  province?: string;
  district?: string;
  category?: JobCategory;
  employmentType?: EmploymentType;
  workMode?: WorkMode;
  sort?: JobSort;
}

export interface JobAdminListQuery extends Omit<JobListQuery, 'sort'> {
  status?: JobAdminFilter;
}

export interface FieldError {
  field: string;
  code: string;
  message: string;
}

export const jobCategories: readonly JobCategory[] = [
  'InformationTechnology',
  'Teaching',
  'Healthcare',
  'Administration',
  'Finance',
  'Sales',
  'Engineering',
  'Construction',
  'Hospitality',
  'Agriculture',
  'Driving',
  'Security',
  'Government',
  'NGO',
  'Other',
];

export const employmentTypes: readonly EmploymentType[] = [
  'FullTime',
  'PartTime',
  'Contract',
  'Internship',
];

export const workModes: readonly WorkMode[] = ['OnSite', 'Remote', 'Hybrid'];

export const jobSorts: readonly JobSort[] = ['Newest', 'Deadline'];

export const jobAdminFilters: readonly JobAdminFilter[] = [
  'All',
  'Published',
  'Draft',
  'Expired',
];

/**
 * The wire values are enum names, which are not what a reader should be shown. These labels are
 * what appear on screen, in a select or a badge.
 */
export const categoryLabels: Record<JobCategory, string> = {
  InformationTechnology: 'Information technology',
  Teaching: 'Teaching',
  Healthcare: 'Healthcare',
  Administration: 'Administration',
  Finance: 'Finance',
  Sales: 'Sales',
  Engineering: 'Engineering',
  Construction: 'Construction',
  Hospitality: 'Hospitality',
  Agriculture: 'Agriculture',
  Driving: 'Driving',
  Security: 'Security',
  Government: 'Government',
  NGO: 'NGO',
  Other: 'Other',
};

export const employmentTypeLabels: Record<EmploymentType, string> = {
  FullTime: 'Full time',
  PartTime: 'Part time',
  Contract: 'Contract',
  Internship: 'Internship',
};

export const workModeLabels: Record<WorkMode, string> = {
  OnSite: 'On site',
  Remote: 'Remote',
  Hybrid: 'Hybrid',
};

export const sortLabels: Record<JobSort, string> = {
  Newest: 'Newest first',
  Deadline: 'Closing soonest',
};

export const adminFilterLabels: Record<JobAdminFilter, string> = {
  All: 'All rows',
  Published: 'Published',
  Draft: 'Drafts',
  Expired: 'Closed',
};

/**
 * A blank row for the editor. The enums default to their first member rather than to nothing,
 * because the API binds an enum and a select cannot present "unset"; a staff member still has to
 * choose a deadline before anything can be published.
 */
export function emptyJobRequest(): SaveJobRequest {
  return {
    slug: '',
    title: '',
    organization: '',
    description: '',
    category: 'InformationTechnology',
    employmentType: 'FullTime',
    workMode: 'OnSite',
    districtCode: null,
    applicationUrl: '',
    applicationDeadline: '',
    isPublished: false,
  };
}