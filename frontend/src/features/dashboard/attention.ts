import type { DashboardSummary } from './types';

export interface QuickLink {
  key: string;
  label: string;
  to: string;
}

export interface AttentionRow {
  key: string;
  label: string;
  count: number;
  to: string;
}

export interface AttentionSection {
  key: 'drafts' | 'stale' | 'reports';
  title: string;
  rows: AttentionRow[];
}

/**
 * The management pages plus the three queues, in the order the modules were built. The
 * dashboard is the one screen that offers all of them at once, which is the point of it:
 * staff land here and go straight to the module they were looking for instead of hunting
 * through the header nav.
 *
 * The targets are the plain `/admin/*` routes. Those pages keep their filters in component
 * state rather than in the URL, so a link carrying `?status=draft` would silently land on
 * the unfiltered list — a link that looks precise and is not.
 */
export const quickLinks: QuickLink[] = [
  { key: 'calculators', label: 'Manage calculators', to: '/admin/calculators' },
  { key: 'quiz', label: 'Manage quiz questions', to: '/admin/quiz' },
  { key: 'students', label: 'Manage resources', to: '/admin/students' },
  { key: 'jobs', label: 'Manage jobs', to: '/admin/jobs' },
  { key: 'businesses', label: 'Manage businesses', to: '/admin/businesses' },
  { key: 'deals', label: 'Manage offers', to: '/admin/deals' },
  { key: 'events', label: 'Manage events', to: '/admin/events' },
  { key: 'reports', label: 'Open the report queue', to: '/admin/reports' },
  { key: 'claims', label: 'Open the claims queue', to: '/admin/claims' },
  { key: 'subscriptionRequests', label: 'Open plan requests', to: '/admin/subscription-requests' },
];

const quickLinkTargets: Record<string, string> = Object.fromEntries(
  quickLinks.map((link) => [link.key, link.to]),
);

/**
 * The rows worth showing, in the three groups a staff member acts on differently:
 * reports nobody has decided on yet, work nobody has published yet, and rows whose
 * publish flag no longer describes what a reader sees.
 *
 * Everything with a count of zero is dropped rather than rendered as "0 — ": on a healthy
 * catalogue most of these are zero, and a list of zeros is noise that trains the eye to
 * ignore the section — which is exactly the section that matters when it is not zero.
 */
export function attentionSections(summary: DashboardSummary): AttentionSection[] {
  const { drafts, staleWhilePublished, unverifiedBusinesses, openReports, pendingClaims, subscriptionRequests } =
    summary.needsAttention;

  const draftRows: AttentionRow[] = [
    { key: 'businesses', label: 'Draft businesses', count: drafts.businesses, to: quickLinkTargets.businesses },
    { key: 'jobs', label: 'Draft jobs', count: drafts.jobs, to: quickLinkTargets.jobs },
    { key: 'deals', label: 'Draft offers', count: drafts.deals, to: quickLinkTargets.deals },
    { key: 'events', label: 'Draft events', count: drafts.events, to: quickLinkTargets.events },
    {
      key: 'studentResources',
      label: 'Draft resources',
      count: drafts.studentResources,
      to: quickLinkTargets.students,
    },
    {
      key: 'quizQuestions',
      label: 'Draft quiz questions',
      count: drafts.quizQuestions,
      to: quickLinkTargets.quiz,
    },
    {
      key: 'calculators',
      label: 'Draft calculators',
      count: drafts.calculators,
      to: quickLinkTargets.calculators,
    },
  ];

  const staleRows: AttentionRow[] = [
    {
      key: 'jobs',
      label: 'Jobs past their deadline, still published',
      count: staleWhilePublished.jobs,
      to: quickLinkTargets.jobs,
    },
    {
      key: 'deals',
      label: 'Offers past their last day, still published',
      count: staleWhilePublished.deals,
      to: quickLinkTargets.deals,
    },
    {
      key: 'studentResources',
      label: 'Resources past their expiry, still published',
      count: staleWhilePublished.studentResources,
      to: quickLinkTargets.students,
    },
    {
      key: 'events',
      label: 'Events that have finished, still published',
      count: staleWhilePublished.events,
      to: quickLinkTargets.events,
    },
    {
      key: 'unverifiedBusinesses',
      label: 'Live listings no longer verified',
      count: unverifiedBusinesses,
      to: quickLinkTargets.businesses,
    },
  ];

  const reportRows: AttentionRow[] = [
    { key: 'openReports', label: 'Open reports', count: openReports, to: quickLinkTargets.reports },
    {
      key: 'pendingClaims',
      label: 'Ownership claims waiting',
      count: pendingClaims,
      to: quickLinkTargets.claims,
    },
    {
      key: 'subscriptionRequests',
      label: 'Plan requests waiting',
      count: subscriptionRequests,
      to: quickLinkTargets.subscriptionRequests,
    },
  ];

  // Reports come first: a member has complained about something and nobody has looked at
  // it yet, which is the only row here with a reader waiting on the other side of it. The
  // two queues behind it are the same kind of work — somebody asked, nobody has answered.
  const sections: AttentionSection[] = [
    { key: 'reports', title: 'Waiting for a decision', rows: reportRows.filter(nonZero) },
    { key: 'drafts', title: 'Waiting to be published', rows: draftRows.filter(nonZero) },
    { key: 'stale', title: 'Published rows that need a decision', rows: staleRows.filter(nonZero) },
  ];

  return sections.filter((section) => section.rows.length > 0);
}

function nonZero(row: AttentionRow): boolean {
  return row.count > 0;
}
