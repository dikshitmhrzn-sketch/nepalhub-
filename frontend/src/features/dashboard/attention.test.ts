import { describe, expect, it } from 'vitest';

import { attentionSections, quickLinks } from './attention';
import type {
  DashboardAttention,
  DashboardCounts,
  DashboardSummary,
  DraftCounts,
  StaleCounts,
} from './types';

const zeroCounts: DashboardCounts = {
  users: 0,
  businesses: 0,
  jobs: 0,
  deals: 0,
  events: 0,
  studentResources: 0,
  quizQuestions: 0,
  calculators: 0,
};

const zeroDrafts: DraftCounts = {
  businesses: 0,
  jobs: 0,
  deals: 0,
  events: 0,
  studentResources: 0,
  quizQuestions: 0,
  calculators: 0,
};

const zeroStale: StaleCounts = { jobs: 0, deals: 0, studentResources: 0, events: 0 };

function summaryWith(attention: Partial<DashboardAttention>): DashboardSummary {
  return {
    counts: zeroCounts,
    needsAttention: {
      drafts: { ...zeroDrafts, ...(attention.drafts ?? {}) },
      staleWhilePublished: { ...zeroStale, ...(attention.staleWhilePublished ?? {}) },
      unverifiedBusinesses: attention.unverifiedBusinesses ?? 0,
      openReports: attention.openReports ?? 0,
      pendingClaims: attention.pendingClaims ?? 0,
      subscriptionRequests: attention.subscriptionRequests ?? 0,
    },
  };
}

describe('attentionSections', () => {
  it('reports nothing at all when every count is zero', () => {
    expect(attentionSections(summaryWith({}))).toEqual([]);
  });

  it('drops the rows with nothing behind them instead of showing zeros', () => {
    const sections = attentionSections(
      summaryWith({ drafts: { ...zeroDrafts, jobs: 3 } }),
    );

    expect(sections).toHaveLength(1);
    expect(sections[0]!.key).toBe('drafts');
    expect(sections[0]!.rows).toHaveLength(1);
    expect(sections[0]!.rows[0]).toEqual({
      key: 'jobs',
      label: 'Draft jobs',
      count: 3,
      to: '/admin/jobs',
    });
  });

  it('keeps drafts and stale rows in their own groups', () => {
    const sections = attentionSections(
      summaryWith({
        drafts: { ...zeroDrafts, calculators: 1 },
        staleWhilePublished: { ...zeroStale, deals: 2 },
        unverifiedBusinesses: 4,
      }),
    );

    expect(sections.map((section) => section.key)).toEqual(['drafts', 'stale']);
    expect(sections[0]!.rows.map((row) => row.key)).toEqual(['calculators']);
    expect(sections[1]!.rows.map((row) => row.key)).toEqual(['deals', 'unverifiedBusinesses']);
    expect(sections[1]!.rows.map((row) => row.count)).toEqual([2, 4]);
  });

  it('never sends a row to a page the quick links do not already offer', () => {
    const targets = new Set(quickLinks.map((link) => link.to));
    const sections = attentionSections(
      summaryWith({
        drafts: { ...zeroDrafts, businesses: 1, jobs: 1, studentResources: 1, quizQuestions: 1 },
        staleWhilePublished: { ...zeroStale, events: 1, studentResources: 1 },
        unverifiedBusinesses: 1,
      }),
    );

    for (const section of sections) {
      for (const row of section.rows) {
        expect(targets.has(row.to)).toBe(true);
      }
    }
  });

  it('offers every management page and queue exactly once', () => {
    expect(quickLinks.map((link) => link.to)).toEqual([
      '/admin/calculators',
      '/admin/quiz',
      '/admin/students',
      '/admin/jobs',
      '/admin/businesses',
      '/admin/deals',
      '/admin/events',
      '/admin/reports',
      '/admin/claims',
      '/admin/subscription-requests',
    ]);
  });

  it('puts open reports in their own section, first and waiting for a decision', () => {
    const sections = attentionSections(summaryWith({ openReports: 5 }));

    expect(sections.map((section) => section.key)).toEqual(['reports']);
    expect(sections[0]!.title).toBe('Waiting for a decision');
    expect(sections[0]!.rows).toEqual([
      { key: 'openReports', label: 'Open reports', count: 5, to: '/admin/reports' },
    ]);
  });

  it('keeps the two decision queues behind the report row, in reading order', () => {
    const sections = attentionSections(
      summaryWith({ openReports: 2, pendingClaims: 3, subscriptionRequests: 4 }),
    );

    expect(sections.map((section) => section.key)).toEqual(['reports']);
    expect(sections[0]!.rows).toEqual([
      { key: 'openReports', label: 'Open reports', count: 2, to: '/admin/reports' },
      {
        key: 'pendingClaims',
        label: 'Ownership claims waiting',
        count: 3,
        to: '/admin/claims',
      },
      {
        key: 'subscriptionRequests',
        label: 'Plan requests waiting',
        count: 4,
        to: '/admin/subscription-requests',
      },
    ]);
  });

  it('drops a queue row with nothing waiting while keeping the other', () => {
    const sections = attentionSections(summaryWith({ pendingClaims: 1 }));

    expect(sections[0]!.rows.map((row) => row.key)).toEqual(['pendingClaims']);
  });

  it('sends every decision row to a page the quick links offer', () => {
    const targets = new Set(quickLinks.map((link) => link.to));
    const sections = attentionSections(
      summaryWith({ openReports: 1, pendingClaims: 1, subscriptionRequests: 1 }),
    );

    for (const row of sections.flatMap((section) => section.rows)) {
      expect(targets.has(row.to)).toBe(true);
    }
  });
});
