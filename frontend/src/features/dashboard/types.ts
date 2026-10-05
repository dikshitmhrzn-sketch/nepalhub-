/** The wire shape of `GET /api/admin/dashboard`, camel-cased by the API's JSON settings. */

export interface DashboardCounts {
  users: number;
  businesses: number;
  jobs: number;
  deals: number;
  events: number;
  studentResources: number;
  quizQuestions: number;
  calculators: number;
}

/** Unpublished rows per module. Accounts have no publish state, so they have none here. */
export interface DraftCounts {
  businesses: number;
  jobs: number;
  deals: number;
  events: number;
  studentResources: number;
  quizQuestions: number;
  calculators: number;
}

/**
 * Rows still flagged published that the API's `PublicContentScope` no longer serves to a
 * reader: past its deadline, past its last day, past its expiry, or finished.
 */
export interface StaleCounts {
  jobs: number;
  deals: number;
  studentResources: number;
  events: number;
}

export interface DashboardAttention {
  drafts: DraftCounts;
  staleWhilePublished: StaleCounts;
  unverifiedBusinesses: number;
  /** Reports nobody has closed yet — the moderation queue's own open total. */
  openReports: number;
  /** Ownership claims nobody has decided — the claims queue's own pending total. */
  pendingClaims: number;
  /** Plan requests nobody has decided — the subscription queue's pending total. */
  subscriptionRequests: number;
}

export interface DashboardSummary {
  counts: DashboardCounts;
  needsAttention: DashboardAttention;
}
