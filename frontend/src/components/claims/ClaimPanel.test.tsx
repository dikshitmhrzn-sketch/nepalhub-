import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useAuth } from '@/features/auth/AuthContext';
import { fileClaim } from '@/features/ownership/api';
import { ApiError } from '@/lib/apiClient';

import { ClaimPanel } from './ClaimPanel';

vi.mock('@/features/auth/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('@/features/ownership/api', () => ({ fileClaim: vi.fn() }));

function authAs(status: 'authenticated' | 'anonymous' | 'loading') {
  vi.mocked(useAuth).mockReturnValue({
    status,
    user:
      status === 'authenticated'
        ? {
            id: 'user-1',
            email: 'owner@example.com',
            fullName: 'Owner One',
            avatarUrl: null,
            isEmailConfirmed: true,
            level: 1,
            xp: 0,
            roles: ['user'],
          }
        : null,
    isAdmin: false,
    isStaff: false,
    signIn: vi.fn(),
    signUp: vi.fn(),
    signOut: vi.fn(),
    reload: vi.fn(),
  });
}

function renderPanel() {
  const router = createMemoryRouter([{ path: '/businesses/:slug', element: <ClaimPanel slug="kath-cafe" /> }], {
    initialEntries: ['/businesses/kath-cafe'],
  });

  render(<RouterProvider router={router} />);
}

describe('ClaimPanel', () => {
  beforeEach(() => {
    authAs('authenticated');
  });

  it('tells a signed-out reader that an account is needed, with the way to get one', () => {
    authAs('anonymous');
    renderPanel();

    expect(screen.getByRole('link', { name: 'Sign in' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Claim this listing' })).toBeNull();
  });

  it('opens the form, sends the claim against the listing, and confirms on screen', async () => {
    vi.mocked(fileClaim).mockResolvedValue({
      id: 'claim-1',
      businessId: 'business-1',
      slug: 'kath-cafe',
      businessName: 'Kath Cafe',
      status: 'Pending',
      evidence: 'I run it',
      createdAt: '2026-10-05T12:00:00Z',
      reviewedAt: null,
      reviewNote: null,
    });

    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this listing' }));
    fireEvent.change(
      screen.getByLabelText('How are you connected to this business? (optional)'),
      { target: { value: '  I run it  ' } },
    );
    fireEvent.click(screen.getByRole('button', { name: 'Send claim' }));

    await waitFor(() => expect(screen.getByText(/Your claim is with our staff/u)).toBeTruthy());
    expect(fileClaim).toHaveBeenCalledWith('kath-cafe', 'I run it');
  });

  it('sends a blank claim when the note is left empty, because that is a legitimate claim', async () => {
    vi.mocked(fileClaim).mockResolvedValue({
      id: 'claim-2',
      businessId: 'business-1',
      slug: 'kath-cafe',
      businessName: 'Kath Cafe',
      status: 'Pending',
      evidence: null,
      createdAt: '2026-10-05T12:00:00Z',
      reviewedAt: null,
      reviewNote: null,
    });

    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this listing' }));
    fireEvent.click(screen.getByRole('button', { name: 'Send claim' }));

    await waitFor(() => expect(fileClaim).toHaveBeenCalledWith('kath-cafe', null));
  });

  it('keeps the form and explains a listing that is already claimed', async () => {
    vi.mocked(fileClaim).mockRejectedValue(
      new ApiError(409, 'already_claimed', 'You already have a claim waiting.'),
    );

    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this listing' }));
    fireEvent.click(screen.getByRole('button', { name: 'Send claim' }));

    expect(
      await screen.findByText('You already have a claim waiting on this listing. Staff are reviewing it.'),
    ).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Send claim' })).toBeTruthy();
  });

  it('lets the reader back out of the form without sending anything', () => {
    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this listing' }));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.getByRole('button', { name: 'Claim this listing' })).toBeTruthy();
    expect(fileClaim).not.toHaveBeenCalled();
  });
});
