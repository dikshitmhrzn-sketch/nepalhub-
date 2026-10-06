/**
 * Mirrors the ownership contracts exposed by the ASP.NET Core API: claiming a listing,
 * the listings a person manages, and the staff queue that decides.
 */

import type { BusinessHours, BusinessCategory, SaveBusinessHours } from '@/features/businesses/types';

/** Where the claim sits in the queue staff work through. The API serialises these by name. */
export type BusinessClaimStatus = 'Pending' | 'Approved' | 'Rejected';

/** One claim as the person who filed it sees it, from the my-business list. */
export interface ClaimView {
  id: string;
  businessId: string;
  slug: string;
  businessName: string;
  status: BusinessClaimStatus;
  evidence: string | null;
  createdAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
}

/** One row of the staff queue: the listing, who claims it, and where it stands. */
export interface AdminClaimRow extends ClaimView {
  claimantName: string;
  claimantEmail: string | null;
}

/** How a person's own listing reads in the list: enough to recognise, never the description. */
export interface MyBusinessSummary {
  id: string;
  slug: string;
  name: string;
  category: BusinessCategory;
  districtName: string;
  isPublished: boolean;
  isVerified: boolean;
}

/** Where a verification request sits in the queue staff work through. */
export type BusinessVerificationStatus = 'Pending' | 'Approved' | 'Rejected';

/**
 * One verification request as the owner sees it, from the my-business list and from the
 * editor's panel. The business name rides along so a row of requests reads as listings
 * rather than as bare ids.
 */
export interface VerificationView {
  id: string;
  businessId: string;
  slug: string;
  businessName: string;
  status: BusinessVerificationStatus;
  note: string | null;
  createdAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
}

/** One row of the staff queue: the listing, who registered it, and where it stands. */
export interface AdminVerificationRow extends VerificationView {
  ownerName: string;
  ownerEmail: string | null;
}

/**
 * One owned listing in the shape the editor sends back. Publish and verify are read-only:
 * those are staff decisions, and the form never offers a control the API would refuse.
 * `verification` is where the owner's own request stands if they have filed one.
 */
export interface OwnerBusiness {
  id: string;
  slug: string;
  name: string;
  description: string;
  category: BusinessCategory;
  districtCode: string;
  districtName: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  isPublished: boolean;
  isVerified: boolean;
  openingHours: BusinessHours[];
  updatedAt: string;
  verification: VerificationView | null;
}

/**
 * What the owner's editor sends back. The identity fields — slug, name, category and
 * district — are here only for a listing that is not published: a draft's owner can correct
 * a typo they made while registering, and once the row is live they belong to staff. Each
 * one is nullable and means "keep as it is", which is how the whole week and the contact
 * details already work.
 */
export interface SaveOwnerBusinessRequest {
  slug?: string | null;
  name?: string | null;
  category?: BusinessCategory | null;
  districtCode?: string | null;
  description: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  openingHours: SaveBusinessHours[];
}

/**
 * Registering a listing of one's own. The row this creates is born owned, unpublished and
 * unverified — the only thing it grants is the authorship of a draft, and everything else
 * is what the verification queue exists to decide.
 */
export interface RegisterBusinessRequest {
  slug: string;
  name: string;
  description: string;
  category: BusinessCategory;
  districtCode: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  openingHours: SaveBusinessHours[];
}

/** The my-business screen: what this person manages, every claim, every verification. */
export interface MyBusinessesResponse {
  owned: MyBusinessSummary[];
  claims: ClaimView[];
  verifications: VerificationView[];
}

/** The queue's status window, as the API's own parser names them. */
export type ClaimFilter = 'all' | 'Pending' | 'Approved' | 'Rejected';

export const claimFilters: readonly ClaimFilter[] = ['all', 'Pending', 'Approved', 'Rejected'];

export const claimFilterLabels: Record<ClaimFilter, string> = {
  all: 'All',
  Pending: 'Open',
  Approved: 'Approved',
  Rejected: 'Rejected',
};

export const claimStatusLabels: Record<BusinessClaimStatus, string> = {
  Pending: 'Waiting for staff',
  Approved: 'Approved',
  Rejected: 'Turned down',
};

/**
 * The query the queue sends: a blank status means no filter and everything else is an
 * exact match, mirroring how the report queue reads the API.
 */
export function claimStatusQuery(filter: ClaimFilter): string | undefined {
  return filter === 'all' ? undefined : filter;
}

/** The verification queue's status window, named the way the claim queue's is. */
export type VerificationFilter = 'all' | 'Pending' | 'Approved' | 'Rejected';

export const verificationFilters: readonly VerificationFilter[] = [
  'all',
  'Pending',
  'Approved',
  'Rejected',
];

export const verificationFilterLabels: Record<VerificationFilter, string> = {
  all: 'All',
  Pending: 'Open',
  Approved: 'Approved',
  Rejected: 'Rejected',
};

export const verificationStatusLabels: Record<BusinessVerificationStatus, string> = {
  Pending: 'Waiting for staff',
  Approved: 'Approved',
  Rejected: 'Turned down',
};

/** A blank status means no filter; anything else is an exact match, as the API reads it. */
export function verificationStatusQuery(filter: VerificationFilter): string | undefined {
  return filter === 'all' ? undefined : filter;
}
