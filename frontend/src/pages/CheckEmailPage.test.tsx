import { useEffect } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter, useNavigate } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { forgotPassword, resendVerification } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';

import { CheckEmailPage } from './CheckEmailPage';

vi.mock('@/features/auth/api', () => ({
  forgotPassword: vi.fn(),
  resendVerification: vi.fn(),
}));

interface CheckEmailState {
  email: string;
  purpose: 'verify' | 'reset';
}

/**
 * The location's state is the only way into this page, and a memory router's initial
 * entries are plain paths — so a seed route navigates with it the way the page that
 * sent the visitor would have.
 */
function Seed({ state }: { state: CheckEmailState | null }) {
  const navigate = useNavigate();

  useEffect(() => {
    navigate('/check-email', { state, replace: true });
  }, [navigate, state]);

  return null;
}

function renderCheckEmail(state: CheckEmailState | null) {
  const router = createMemoryRouter(
    [
      { path: '/', element: <Seed state={state} /> },
      { path: '/check-email', element: <CheckEmailPage /> },
      { path: '/login', element: <h1>Sign in</h1> },
    ],
    { initialEntries: ['/'] },
  );

  render(<RouterProvider router={router} />);
}

describe('CheckEmailPage', () => {
  beforeEach(() => {
    vi.mocked(resendVerification).mockReset();
    vi.mocked(forgotPassword).mockReset();
  });

  it('sends a visitor with nothing to check for back to sign in', async () => {
    renderCheckEmail(null);

    expect(await screen.findByText('Sign in')).toBeTruthy();
  });

  it('offers a fresh confirmation link for an address still waiting on one', async () => {
    vi.mocked(resendVerification).mockResolvedValue({ sent: true });
    renderCheckEmail({ email: 'sita@nepalhub.test', purpose: 'verify' });

    expect(await screen.findByText('Confirm your email')).toBeTruthy();
    expect(screen.getByText(/sita@nepalhub\.test/u)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Send it again' }));

    expect(await screen.findByText(/Another link is on its way/u)).toBeTruthy();
    expect(vi.mocked(resendVerification)).toHaveBeenCalledWith({ email: 'sita@nepalhub.test' });
    expect(vi.mocked(forgotPassword)).not.toHaveBeenCalled();
  });

  it('re-asks the recovery endpoint when the wait is for a reset link', async () => {
    vi.mocked(forgotPassword).mockResolvedValue({ sent: true });
    renderCheckEmail({ email: 'sita@nepalhub.test', purpose: 'reset' });

    expect(await screen.findByText('Check your email')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Send it again' }));

    expect(await screen.findByText(/Another link is on its way/u)).toBeTruthy();
    expect(vi.mocked(forgotPassword)).toHaveBeenCalledWith({ email: 'sita@nepalhub.test' });
    expect(vi.mocked(resendVerification)).not.toHaveBeenCalled();
  });

  it('surfaces a quota refusal rather than claiming the link went', async () => {
    vi.mocked(resendVerification).mockRejectedValue(
      new ApiError(429, 'rate_limited', 'Too many requests.'),
    );
    renderCheckEmail({ email: 'sita@nepalhub.test', purpose: 'verify' });

    fireEvent.click(await screen.findByRole('button', { name: 'Send it again' }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Too many attempts');
  });
});
