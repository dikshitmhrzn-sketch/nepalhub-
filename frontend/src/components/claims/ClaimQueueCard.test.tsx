import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { reviewClaim } from '@/features/ownership/api';
import type { AdminClaimRow } from '@/features/ownership/types';
import { ApiError } from '@/lib/apiClient';

import { ClaimQueueCard } from './ClaimQueueCard';

vi.mock('@/features/ownership/api', () => ({ reviewClaim: vi.fn() }));

const row: AdminClaimRow = {
  id: 'claim-1',
  businessId: 'business-1',
  slug: 'kath-cafe',
  businessName: 'Kath Cafe',
  claimantName: 'Owner One',
  claimantEmail: 'owner@example.com',
  status: 'Pending',
  evidence: 'I run it',
  createdAt: '2026-10-01T12:00:00Z',
  reviewedAt: null,
  reviewNote: null,
};

function renderCard(overrides: Partial<AdminClaimRow> = {}, onChanged = vi.fn()) {
  const router = createMemoryRouter(
    [{ path: '/claims', element: <ClaimQueueCard row={{ ...row, ...overrides }} onChanged={onChanged} /> }],
    { initialEntries: ['/claims'] },
  );

  render(<RouterProvider router={router} />);

  return { onChanged };
}

describe('ClaimQueueCard', () => {
  beforeEach(() => {
    vi.mocked(reviewClaim).mockReset();
  });

  it('approves an open claim with the note the moderator typed', async () => {
    vi.mocked(reviewClaim).mockResolvedValue({ ...row, status: 'Approved' });
    const { onChanged } = renderCard();

    fireEvent.change(screen.getByLabelText('Decision note (optional)'), {
      target: { value: 'Phone number matches' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(reviewClaim).toHaveBeenCalledWith('claim-1', 'approve', 'Phone number matches'));
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('sends a blank note when none was written, because the note is optional', async () => {
    vi.mocked(reviewClaim).mockResolvedValue({ ...row, status: 'Rejected' });
    renderCard();

    fireEvent.click(screen.getByRole('button', { name: 'Reject' }));

    await waitFor(() => expect(reviewClaim).toHaveBeenCalledWith('claim-1', 'reject', null));
  });

  it('keeps the decision on screen once the claim is settled', () => {
    renderCard({
      status: 'Approved',
      reviewedAt: '2026-10-02T12:00:00Z',
      reviewNote: 'Confirmed by phone',
    });

    expect(screen.getByText('Approved')).toBeTruthy();
    expect(screen.getByText(/Confirmed by phone/u)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('reloads the queue when another moderator got there first', async () => {
    vi.mocked(reviewClaim).mockRejectedValue(new ApiError(409, 'claim_closed', 'Already decided.'));
    const { onChanged } = renderCard();

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

    expect(await screen.findByText('Another moderator has already decided this claim.')).toBeTruthy();
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('links the row to the listing it is about', () => {
    renderCard();

    const link = screen.getByRole('link', { name: 'Kath Cafe' });
    expect(link.getAttribute('href')).toBe('/businesses/kath-cafe');
  });
});
