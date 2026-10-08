import { fireEvent, render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { resetPassword } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';

import { ResetPasswordPage } from './ResetPasswordPage';

vi.mock('@/features/auth/api', () => ({
  resetPassword: vi.fn(),
}));

function renderReset(query = '?email=sita%40nepalhub.test&token=the-token') {
  const router = createMemoryRouter(
    [{ path: '/reset-password', element: <ResetPasswordPage /> }],
    { initialEntries: [`/reset-password${query}`] },
  );

  render(<RouterProvider router={router} />);
}

async function fillAndSubmit(password: string, confirmation: string) {
  fireEvent.change(await screen.findByLabelText('New password'), {
    target: { value: password },
  });
  fireEvent.change(screen.getByLabelText('Confirm new password'), {
    target: { value: confirmation },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Set the new password' }));
}

describe('ResetPasswordPage', () => {
  beforeEach(() => {
    vi.mocked(resetPassword).mockReset();
  });

  it('spends the link on a password both fields agree on', async () => {
    vi.mocked(resetPassword).mockResolvedValue({ reset: true });
    renderReset();

    await fillAndSubmit('BrandNewPass1!', 'BrandNewPass1!');

    expect(await screen.findByText('Password changed')).toBeTruthy();
    expect(vi.mocked(resetPassword)).toHaveBeenCalledWith({
      email: 'sita@nepalhub.test',
      token: 'the-token',
      password: 'BrandNewPass1!',
    });
  });

  it('does not spend the link when the two fields disagree', async () => {
    renderReset();

    await fillAndSubmit('BrandNewPass1!', 'SomethingElse1!');

    expect(await screen.findByText('The two passwords do not match.')).toBeTruthy();
    expect(vi.mocked(resetPassword)).not.toHaveBeenCalled();
  });

  it('reports a password the policy refused next to the input that holds it', async () => {
    vi.mocked(resetPassword).mockRejectedValue(
      new ApiError(400, 'validation_failed', 'The password could not be used.', [
        { field: 'password', code: 'password_too_short', message: 'Password must be at least 8 characters.' },
      ]),
    );
    renderReset();

    await fillAndSubmit('short', 'short');

    expect(await screen.findByText('Password must be at least 8 characters.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Set the new password' })).toBeTruthy();
  });

  it('shows a refused link as refused, with the way to get a new one', async () => {
    vi.mocked(resetPassword).mockRejectedValue(
      new ApiError(400, 'token_invalid', 'This link is invalid or has expired.'),
    );
    renderReset();

    await fillAndSubmit('BrandNewPass1!', 'BrandNewPass1!');

    expect(await screen.findByText('That link no longer works')).toBeTruthy();
    const askAgain = screen.getByRole('link', { name: 'Ask for a new link' });
    expect(askAgain.getAttribute('href')).toBe('/forgot-password');
  });

  it('says the link is incomplete when the url arrived without one', async () => {
    renderReset('?email=sita%40nepalhub.test');

    expect(await screen.findByText('This link is incomplete')).toBeTruthy();
    expect(screen.queryByLabelText('New password')).toBeNull();
    expect(vi.mocked(resetPassword)).not.toHaveBeenCalled();
  });
});
