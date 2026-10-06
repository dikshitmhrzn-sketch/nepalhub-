import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ApiError } from '@/lib/apiClient';

import { getStaffDashboard } from './api';
import { useStaffDashboard } from './useStaffDashboard';
import type { DashboardSummary } from './types';

vi.mock('./api', () => ({ getStaffDashboard: vi.fn() }));

const summary: DashboardSummary = {
  counts: {
    users: 3,
    businesses: 25,
    jobs: 16,
    deals: 15,
    events: 12,
    studentResources: 24,
    quizQuestions: 31,
    calculators: 3,
  },
  needsAttention: {
    drafts: {
      businesses: 0,
      jobs: 2,
      deals: 0,
      events: 1,
      studentResources: 0,
      quizQuestions: 4,
      calculators: 0,
    },
    staleWhilePublished: { jobs: 2, deals: 1, studentResources: 2, events: 0 },
    unverifiedBusinesses: 0,
    openReports: 0,
    pendingClaims: 0,
    pendingVerifications: 0,
    subscriptionRequests: 0,
  },
};

describe('useStaffDashboard', () => {
  it('starts in the loading state and settles on the payload', async () => {
    vi.mocked(getStaffDashboard).mockResolvedValue(summary);

    const { result } = renderHook(() => useStaffDashboard());

    expect(result.current.status).toBe('loading');

    await waitFor(() => expect(result.current.status).toBe('success'));
    expect(result.current.data).toEqual(summary);
    expect(getStaffDashboard).toHaveBeenCalledTimes(1);
  });

  it('reports an ApiError rather than a raw failure', async () => {
    vi.mocked(getStaffDashboard).mockRejectedValue(
      new ApiError(0, 'network_error', 'Could not reach the server. Check your connection.'),
    );

    const { result } = renderHook(() => useStaffDashboard());

    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.error?.code).toBe('network_error');
    expect(result.current.data).toBeNull();
  });

  it('asks again when the reader retries after a failure', async () => {
    vi.mocked(getStaffDashboard)
      .mockRejectedValueOnce(new ApiError(500, 'server_error', 'Something went wrong.'))
      .mockResolvedValueOnce(summary);

    const { result } = renderHook(() => useStaffDashboard());

    await waitFor(() => expect(result.current.status).toBe('error'));

    result.current.reload();

    await waitFor(() => expect(result.current.status).toBe('success'));
    expect(result.current.data).toEqual(summary);
    expect(getStaffDashboard).toHaveBeenCalledTimes(2);
  });

  it('abandons the request when the page unmounts mid-flight', async () => {
    let resolveRequest: ((value: DashboardSummary) => void) | undefined;
    vi.mocked(getStaffDashboard).mockImplementation(
      (signal) =>
        new Promise<DashboardSummary>((resolve, reject) => {
          resolveRequest = resolve;
          signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
        }),
    );

    const { result, unmount } = renderHook(() => useStaffDashboard());
    expect(result.current.status).toBe('loading');

    unmount();

    // A response that lands after the page is gone must not try to set state on it.
    resolveRequest?.(summary);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(result.current.status).toBe('loading');
  });
});
