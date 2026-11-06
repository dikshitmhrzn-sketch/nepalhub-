/**
 * Mirrors the content-claim contracts exposed by the ASP.NET Core API: claiming a
 * published job, offer, event or business, the claimant's own list, and the poster's decision.
 */

/** Which kind of post a claim is against. The API serialises these by name. */
export type ContentClaimKind = 'Job' | 'Event' | 'Deal' | 'Business';

/** Where a content claim sits in the poster's decision. */
export type ContentClaimStatus = 'Pending' | 'Accepted' | 'Released';

/** One claim as the person who filed it sees it. */
export interface MyContentClaim {
  id: string;
  kind: ContentClaimKind;
  slug: string;
  title: string;
  status: ContentClaimStatus;
  note: string | null;
  createdAt: string;
  reviewedAt: string | null;
  reviewNote: string | null;
}

/** One claim on a post this person created: who claimed it, and where it stands. */
export interface PostedContentClaim extends MyContentClaim {
  claimantName: string;
  claimantEmail: string | null;
}

/** Both account boxes in one read. */
export interface ContentClaimsOverview {
  mine: MyContentClaim[];
  onMyPosts: PostedContentClaim[];
}

export const contentClaimKindLabels: Record<ContentClaimKind, string> = {
  Job: 'Job',
  Event: 'Event',
  Deal: 'Offer',
  Business: 'Business',
};

export const contentClaimStatusLabels: Record<ContentClaimStatus, string> = {
  Pending: 'Waiting for the poster',
  Accepted: 'Accepted',
  Released: 'Released',
};

/** The path segment for filing a claim, matching the module's public route. */
export function claimPathFor(kind: ContentClaimKind): string {
  switch (kind) {
    case 'Job':
      return 'jobs';
    case 'Event':
      return 'events';
    case 'Business':
      return 'businesses';
    default:
      return 'deals';
  }
}
