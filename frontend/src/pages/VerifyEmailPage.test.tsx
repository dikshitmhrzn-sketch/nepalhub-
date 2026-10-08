import { fireEvent, render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { resendVerification, verifyEmail } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';

import { VerifyEmailPage } from './VerifyEmailPage';

vi.mock('@/features/auth/api', () => ({
  verifyEmail: vi.fn(),
  resendVerification: vi.fn(),
}));

function renderVerify(query = '?email=sita%40nepalhub.test&token=the-token') {
  const router = createMemoryRouter(
    [{ path: '/verify-email', element: <VerifyEmailPage /> }],
    { initialEntries: [`/verify-email${query}`] },
  );

  render(<RouterProvider router={router} />);
}

describe('VerifyEmailPage', () => {
  beforeEach(() => {
    vi.mocked(verifyEmail).mockReset();
    vi.mocked(resendVerification).mockReset();
  });

  it('confirms on arrival with the address and token the mail carried', async () => {
    vi.mocked(verifyEmail).mockResolvedValue({ verified: true });
    renderVerify();

    expect(await screen.findByText('Email confirmed')).toBeTruthy();
    expect(vi.mocked(verifyEmail)).toHaveBeenCalledWith({
      email: 'sita@nepalhub.test',
      token: 'the-token',
    });
  });

  it('offers a fresh link when the one in the url was refused', async () => {
    vi.mocked(verifyEmail).mockRejectedValue(
      new ApiError(400, 'token_invalid', 'This link is invalid or has expired.'),
    );
    vi.mocked(resendVerification).mockResolvedValue({ sent: true });
    renderVerify();

    expect(await screen.findByText('That link no longer works')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Send a new link' }));

    expect(await screen.findByText(/A new link is on its way/u)).toBeTruthy();
    expect(vi.mocked(resendVerification)).toHaveBeenCalledWith({ email: 'sita@nepalhub.test' });
  });

  it('never posts a link whose parts are missing from the url', async () => {
    renderVerify('');

    expect(await screen.findByText('That link no longer works')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Send a new link' })).toBeNull();
    expect(vi.mocked(verifyEmail)).not.toHaveBeenCalled();
  });
});
