import { render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { emptyJobRequest, type SaveJobRequest } from '@/features/jobs/types';
import type { ReferenceState } from '@/features/reference/useReferenceData';
import { getSubmission } from '@/features/submissions/api';
import type { SubmissionDetail, SubmissionView } from '@/features/submissions/types';
import { useMySubmissions } from '@/features/submissions/useMySubmissions';
import { useReferenceData } from '@/features/reference/useReferenceData';

import { SubmitPage } from './SubmitPage';

let authStatus: 'authenticated' | 'anonymous' = 'authenticated';

vi.mock('@/features/auth/AuthContext', () => ({
  useAuth: () => ({ status: authStatus }),
}));

vi.mock('@/features/submissions/useMySubmissions', () => ({ useMySubmissions: vi.fn() }));
vi.mock('@/features/reference/useReferenceData', () => ({ useReferenceData: vi.fn() }));
vi.mock('@/features/submissions/api', () => ({
  getSubmission: vi.fn(),
  submitJobSubmission: vi.fn(),
  submitEventSubmission: vi.fn(),
  submitDealSubmission: vi.fn(),
  updateSubmission: vi.fn(),
  resubmitSubmission: vi.fn(),
  reviewSubmission: vi.fn(),
}));

const reference: ReferenceState & { reload: () => void } = {
  status: 'success',
  data: { provinces: [], districtsByProvince: {} },
  error: null,
  reload: vi.fn(),
};

const pending: SubmissionView = {
  id: 'submission-1',
  kind: 'Job',
  itemId: 'job-1',
  slug: 'junior-python-developer',
  title: 'Junior Python Developer',
  isPublished: false,
  status: 'Pending',
  note: 'My first posting',
  createdAt: '2026-10-01T12:00:00Z',
  reviewedAt: null,
  reviewNote: null,
};

const rejected: SubmissionView = {
  id: 'submission-2',
  kind: 'Event',
  itemId: 'event-1',
  slug: 'kathmandu-meetup',
  title: 'Kathmandu Meetup',
  isPublished: false,
  status: 'Rejected',
  note: null,
  createdAt: '2026-10-02T12:00:00Z',
  reviewedAt: '2026-10-03T12:00:00Z',
  reviewNote: 'The date had already passed',
};

const approved: SubmissionView = {
  id: 'submission-3',
  kind: 'Deal',
  itemId: 'deal-1',
  slug: 'tea-for-two',
  title: 'Tea for two',
  isPublished: true,
  status: 'Approved',
  note: null,
  createdAt: '2026-09-20T12:00:00Z',
  reviewedAt: '2026-09-21T12:00:00Z',
  reviewNote: null,
};

const jobContent: SaveJobRequest = {
  ...emptyJobRequest(),
  slug: pending.slug,
  title: pending.title,
  organization: 'Acme',
};

function renderPage(submissions: SubmissionView[] = [], entry = '/submit') {
  vi.mocked(useMySubmissions).mockReturnValue({
    status: 'success',
    data: submissions,
    error: null,
    reload: vi.fn(),
  });
  vi.mocked(useReferenceData).mockReturnValue(reference);

  const router = createMemoryRouter([{ path: '/submit', element: <SubmitPage /> }], {
    initialEntries: [entry],
  });

  render(<RouterProvider router={router} />);
}

describe('SubmitPage', () => {
  beforeEach(() => {
    authStatus = 'authenticated';
    vi.mocked(getSubmission).mockReset();
  });

  it('asks a visitor to sign in rather than showing a form nobody can send', () => {
    authStatus = 'anonymous';
    renderPage();

    expect(screen.getByText('Sign in to send something in')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Sign in' }).getAttribute('href')).toBe('/login');
    expect(screen.queryByRole('button', { name: 'Event' })).toBeNull();
  });

  it('offers the three kinds, with a job form waiting, and says nothing has been sent yet', () => {
    renderPage();

    const jobTab = screen.getByRole('button', { name: 'Job' });
    expect(jobTab.getAttribute('aria-pressed')).toBe('true');
    expect(screen.getByRole('button', { name: 'Event' }).getAttribute('aria-pressed')).toBe('false');
    expect(screen.getByRole('button', { name: 'Offer' })).toBeTruthy();

    // The draft form carries no publish switch — going live is the queue's decision.
    expect(screen.getByRole('heading', { name: 'Send a job for review' })).toBeTruthy();
    expect(screen.queryByRole('checkbox')).toBeNull();

    expect(screen.getByText(/Nothing yet/u)).toBeTruthy();
  });

  it('shows each request with the actions its standing allows', () => {
    renderPage([pending, rejected, approved]);

    // Waiting and turned down can both be corrected; an approved one cannot.
    expect(screen.getAllByRole('button', { name: 'Edit' })).toHaveLength(2);

    // Only a rejection can be sent again.
    expect(screen.getByRole('button', { name: 'Send again' })).toBeTruthy();

    // Only the live one links out, and it links where it actually lives.
    const viewLinks = screen.getAllByRole('link', { name: 'View' });
    expect(viewLinks).toHaveLength(1);
    expect(viewLinks[0]!.getAttribute('href')).toBe('/deals/tea-for-two');

    expect(screen.getByText('Waiting for staff')).toBeTruthy();
    expect(screen.getByText(/The date had already passed/u)).toBeTruthy();
    expect(screen.getByText(/sent 10\/1\/2026/u)).toBeTruthy();
  });

  it('corrects a saved draft at its edit link instead of composing a second one', async () => {
    const detail: SubmissionDetail = { submission: pending, job: jobContent, event: null, deal: null };
    vi.mocked(getSubmission).mockResolvedValue(detail);

    renderPage([pending], '/submit?edit=submission-1');

    expect(screen.getByRole('heading', { name: 'Correct a draft', level: 2 })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Event' })).toBeNull();

    // A pending draft needs no explanation of a rejection, and loads its own body back.
    expect(screen.queryByText(/press Send again on its row below/u)).toBeNull();
    expect(await screen.findByRole('heading', { name: 'Edit your draft' })).toBeTruthy();
  });
});
