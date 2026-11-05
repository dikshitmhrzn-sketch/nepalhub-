import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useAuth } from '@/features/auth/AuthContext';
import { fileContentClaim } from '@/features/claims/api';
import { ApiError } from '@/lib/apiClient';

import { ContentClaimPanel } from './ContentClaimPanel';

vi.mock('@/features/auth/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('@/features/claims/api', () => ({ fileContentClaim: vi.fn() }));

function authAs(status: 'authenticated' | 'anonymous' | 'loading') {
  vi.mocked(useAuth).mockReturnValue({
    status,
    user:
      status === 'authenticated'
        ? {
            id: 'user-1',
            email: 'member@example.com',
            fullName: 'Member One',
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
  const router = createMemoryRouter(
    [{ path: '/jobs/:slug', element: <ContentClaimPanel kind="Job" slug="staff-job" /> }],
    { initialEntries: ['/jobs/staff-job'] },
  );

  render(<RouterProvider router={router} />);
}

describe('ContentClaimPanel', () => {
  beforeEach(() => {
    authAs('authenticated');
  });

  it('tells a signed-out reader that an account is needed', () => {
    authAs('anonymous');
    renderPanel();

    expect(screen.getByRole('link', { name: 'Sign in' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Claim this post' })).toBeNull();
  });

  it('opens the form, sends the claim, and points at My claims', async () => {
    vi.mocked(fileContentClaim).mockResolvedValue({
      id: 'claim-1',
      kind: 'Job',
      slug: 'staff-job',
      title: 'A test posting',
      status: 'Pending',
      note: 'Mine',
      createdAt: '2026-11-05T12:00:00Z',
      reviewedAt: null,
      reviewNote: null,
    });

    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this post' }));
    fireEvent.change(screen.getByLabelText('Why is this yours? (optional)'), {
      target: { value: '  Mine  ' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Send claim' }));

    await waitFor(() => expect(screen.getByText(/Claim filed/u)).toBeTruthy());
    expect(fileContentClaim).toHaveBeenCalledWith('Job', 'staff-job', 'Mine');
    expect(screen.getByRole('link', { name: 'My claims' })).toBeTruthy();
  });

  it('sends a blank note when the field is left empty', async () => {
    vi.mocked(fileContentClaim).mockResolvedValue({
      id: 'claim-2',
      kind: 'Job',
      slug: 'staff-job',
      title: 'A test posting',
      status: 'Pending',
      note: null,
      createdAt: '2026-11-05T12:00:00Z',
      reviewedAt: null,
      reviewNote: null,
    });

    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this post' }));
    fireEvent.click(screen.getByRole('button', { name: 'Send claim' }));

    await waitFor(() => expect(fileContentClaim).toHaveBeenCalledWith('Job', 'staff-job', null));
  });

  it('keeps the form and explains a post someone else already claimed', async () => {
    vi.mocked(fileContentClaim).mockRejectedValue(
      new ApiError(409, 'already_taken', 'Someone already claimed this post.'),
    );

    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this post' }));
    fireEvent.click(screen.getByRole('button', { name: 'Send claim' }));

    expect(await screen.findByText('Someone already claimed this post.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Send claim' })).toBeTruthy();
  });

  it('lets the reader back out without sending anything', () => {
    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: 'Claim this post' }));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.getByRole('button', { name: 'Claim this post' })).toBeTruthy();
    expect(fileContentClaim).not.toHaveBeenCalled();
  });
});
