import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { getStaffDashboard } from '@/features/dashboard/api';
import type { DashboardSummary } from '@/features/dashboard/types';
import { ApiError } from '@/lib/apiClient';

import { StaffDashboardPage } from './StaffDashboardPage';

vi.mock('@/features/dashboard/api', () => ({ getStaffDashboard: vi.fn() }));

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
      businesses: 1,
      jobs: 2,
      deals: 0,
      events: 0,
      studentResources: 0,
      quizQuestions: 4,
      calculators: 0,
    },
    staleWhilePublished: { jobs: 2, deals: 1, studentResources: 0, events: 0 },
    unverifiedBusinesses: 0,
    openReports: 0,
    pendingClaims: 0,
    subscriptionRequests: 0,
  },
};

function renderPage() {
  const router = createMemoryRouter([{ path: '/admin', element: <StaffDashboardPage /> }], {
    initialEntries: ['/admin'],
  });

  render(<RouterProvider router={router} />);
}

describe('StaffDashboardPage', () => {
  it('shows all eight counts, each on its own card', async () => {
    vi.mocked(getStaffDashboard).mockResolvedValue(summary);
    renderPage();

    await waitFor(() => expect(screen.getByRole('heading', { level: 1 })).toBeTruthy());

    const cards: [string, string][] = [
      ['Users', '3'],
      ['Businesses', '25'],
      ['Jobs', '16'],
      ['Offers', '15'],
      ['Events', '12'],
      ['Student resources', '24'],
      ['Quiz questions', '31'],
      ['Calculators', '3'],
    ];

    for (const [label, value] of cards) {
      const card = screen.getByText(label).closest('li');
      expect(card?.textContent).toContain(value);
    }
  });

  it('links to every management page from the quick links', async () => {
    vi.mocked(getStaffDashboard).mockResolvedValue(summary);
    renderPage();

    await waitFor(() => expect(screen.getByRole('link', { name: 'Manage jobs' })).toBeTruthy());

    const links: [string, string][] = [
      ['Manage calculators', '/admin/calculators'],
      ['Manage quiz questions', '/admin/quiz'],
      ['Manage resources', '/admin/students'],
      ['Manage jobs', '/admin/jobs'],
      ['Manage businesses', '/admin/businesses'],
      ['Manage offers', '/admin/deals'],
      ['Manage events', '/admin/events'],
    ];

    for (const [name, href] of links) {
      const link = screen.getByRole('link', { name });
      expect(link.getAttribute('href')).toBe(href);
    }
  });

  it('shows only the rows that have something behind them', async () => {
    vi.mocked(getStaffDashboard).mockResolvedValue(summary);
    renderPage();

    await waitFor(() => expect(screen.getByRole('link', { name: 'Draft jobs' })).toBeTruthy());

    expect(screen.getByRole('link', { name: 'Draft businesses' }).getAttribute('href')).toBe(
      '/admin/businesses',
    );
    expect(screen.getByText('Draft quiz questions')).toBeTruthy();
    expect(screen.getByText('Jobs past their deadline, still published')).toBeTruthy();

    // Zero is not a thing that needs a decision, so it is not a row.
    expect(screen.queryByText('Draft calculators')).toBeNull();
    expect(screen.queryByText('Events that have finished, still published')).toBeNull();
    expect(screen.queryByText('Live listings no longer verified')).toBeNull();
  });

  it('says so plainly when nothing needs attention', async () => {
    vi.mocked(getStaffDashboard).mockResolvedValue({
      ...summary,
      needsAttention: {
        drafts: {
          businesses: 0,
          jobs: 0,
          deals: 0,
          events: 0,
          studentResources: 0,
          quizQuestions: 0,
          calculators: 0,
        },
        staleWhilePublished: { jobs: 0, deals: 0, studentResources: 0, events: 0 },
        unverifiedBusinesses: 0,
        openReports: 0,
        pendingClaims: 0,
        subscriptionRequests: 0,
      },
    });
    renderPage();

    await waitFor(() => expect(screen.getByText('Nothing needs attention')).toBeTruthy());

    // The quick links are navigation, not a status, so they stay on screen either way.
    expect(screen.getByRole('link', { name: 'Manage events' })).toBeTruthy();
  });

  it('offers a retry when the counts cannot be loaded', async () => {
    vi.mocked(getStaffDashboard)
      .mockRejectedValueOnce(new ApiError(0, 'network_error', 'Could not reach the server.'))
      .mockResolvedValueOnce(summary);
    renderPage();

    await waitFor(() => expect(screen.getByText('Could not load the dashboard')).toBeTruthy());
    expect(screen.getByText('Could not reach the server.')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(screen.getByText('Draft jobs')).toBeTruthy());
    expect(getStaffDashboard).toHaveBeenCalledTimes(2);
  });
});
