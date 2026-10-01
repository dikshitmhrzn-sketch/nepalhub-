/** Mirrors the Student Hub contracts exposed by the ASP.NET Core API. */

/** The API serialises enums by name, so these are the literal wire values. */
export type ResourceType = 'Note' | 'QuestionPaper' | 'Scholarship';

export type ResourceCategory =
  | 'General'
  | 'Mathematics'
  | 'Science'
  | 'English'
  | 'Nepali'
  | 'SocialStudies'
  | 'ComputerScience'
  | 'Management';

export type EducationLevel =
  | 'General'
  | 'Grade6to8'
  | 'Grade9to10'
  | 'Grade11to12'
  | 'PlusTwo'
  | 'Bachelors'
  | 'Competitive';

export type ResourceSort = 'Newest' | 'Expiring' | 'Title';

/** The staff list filter, which includes expired rows that readers cannot see. */
export type ResourceAdminFilter = 'All' | 'Published' | 'Draft' | 'Expired';

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
 * One row on a catalogue card. The body is deliberately absent: a card is one line of
 * prose, and the detail endpoint is where the content comes from.
 */
export interface ResourceSummary {
  id: string;
  slug: string;
  type: ResourceType;
  category: ResourceCategory;
  level: EducationLevel;
  title: string;
  summary: string;
  hasExternalUrl: boolean;
  expiresOn: string | null;
  updatedAt: string;
}

/** One resource in full, for a reader who has opened it. */
export interface ResourceDetail extends Omit<ResourceSummary, 'hasExternalUrl'> {
  body: string;
  externalUrl: string | null;
  academicYear: number | null;
  provider: string | null;
  awardAmountNpr: number | null;
  eligibility: string | null;
  isExpired: boolean;
}

/** The staff list row, which shows drafts and expired rows too. */
export interface ResourceAdminSummary {
  id: string;
  slug: string;
  type: ResourceType;
  category: ResourceCategory;
  level: EducationLevel;
  title: string;
  isPublished: boolean;
  isExpired: boolean;
  expiresOn: string | null;
  updatedAt: string;
  createdByEmail: string | null;
}

/** The staff view of one row, which also says what is still missing before publishing. */
export interface ResourceAdmin extends Omit<ResourceDetail, 'isExpired'> {
  isExpired: boolean;
  isPublished: boolean;
  createdAt: string;
  createdByEmail: string | null;
  publishBlockers: string[];
}

/** Create and update share one body, so the staff form sends the same shape for both. */
export interface SaveResourceRequest {
  slug: string;
  title: string;
  summary: string;
  body: string;
  type: ResourceType;
  category: ResourceCategory;
  level: EducationLevel;
  externalUrl: string | null;
  academicYear: number | null;
  provider: string | null;
  awardAmountNpr: number | null;
  eligibility: string | null;
  expiresOn: string | null;
  isPublished: boolean;
}

export interface ResourceListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  type?: ResourceType;
  category?: ResourceCategory;
  level?: EducationLevel;
  sort?: ResourceSort;
}

export interface ResourceAdminListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  type?: ResourceType;
  category?: ResourceCategory;
  level?: EducationLevel;
  status?: ResourceAdminFilter;
}

export const resourceTypes: readonly ResourceType[] = ['Note', 'QuestionPaper', 'Scholarship'];

export const resourceCategories: readonly ResourceCategory[] = [
  'General',
  'Mathematics',
  'Science',
  'English',
  'Nepali',
  'SocialStudies',
  'ComputerScience',
  'Management',
];

export const educationLevels: readonly EducationLevel[] = [
  'General',
  'Grade6to8',
  'Grade9to10',
  'Grade11to12',
  'PlusTwo',
  'Bachelors',
  'Competitive',
];

export const resourceSorts: readonly ResourceSort[] = ['Newest', 'Expiring', 'Title'];

export const resourceAdminFilters: readonly ResourceAdminFilter[] = [
  'All',
  'Published',
  'Draft',
  'Expired',
];

/**
 * The wire values are enum names, which are not what a reader should be shown.
 * These labels are what appear on screen, in a select or a badge.
 */
export const typeLabels: Record<ResourceType, string> = {
  Note: 'Note',
  QuestionPaper: 'Question paper',
  Scholarship: 'Scholarship',
};

export const categoryLabels: Record<ResourceCategory, string> = {
  General: 'General',
  Mathematics: 'Mathematics',
  Science: 'Science',
  English: 'English',
  Nepali: 'Nepali',
  SocialStudies: 'Social studies',
  ComputerScience: 'Computer science',
  Management: 'Management',
};

export const levelLabels: Record<EducationLevel, string> = {
  General: 'All levels',
  Grade6to8: 'Grades 6 to 8',
  Grade9to10: 'Grades 9 to 10',
  Grade11to12: 'Grades 11 to 12',
  PlusTwo: 'Plus two',
  Bachelors: 'Bachelor’s',
  Competitive: 'Competitive exams',
};

export const sortLabels: Record<ResourceSort, string> = {
  Newest: 'Newest first',
  Expiring: 'Closing soonest',
  Title: 'Title A to Z',
};

/** A blank row for the editor, with nothing that would block publishing. */
export function emptyResourceRequest(): SaveResourceRequest {
  return {
    slug: '',
    title: '',
    summary: '',
    body: '',
    type: 'Note',
    category: 'General',
    level: 'General',
    externalUrl: null,
    academicYear: null,
    provider: null,
    awardAmountNpr: null,
    eligibility: null,
    expiresOn: null,
    isPublished: false,
  };
}
