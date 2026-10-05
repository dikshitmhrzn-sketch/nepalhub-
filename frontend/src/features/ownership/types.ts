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

/**
 * One owned listing in the shape the editor sends back. Publish and verify are read-only:
 * those are staff decisions, and the form never offers a control the API would refuse.
 */
export interface OwnerBusiness {
  id: string;
  slug: string;
  name: string;
  description: string;
  category: BusinessCategory;
  districtName: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  isPublished: boolean;
  isVerified: boolean;
  openingHours: BusinessHours[];
  updatedAt: string;
}

/**
 * What the owner's editor sends back. Deliberately narrow: no slug, name, category, district,
 * publish flag or verify badge, because those belong to staff.
 */
export interface SaveOwnerBusinessRequest {
  description: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  openingHours: SaveBusinessHours[];
}

/** The my-business screen: what this person manages, and every claim they have filed. */
export interface MyBusinessesResponse {
  owned: MyBusinessSummary[];
  claims: ClaimView[];
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
