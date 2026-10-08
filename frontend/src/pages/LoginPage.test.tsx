import { fireEvent, render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { resendVerification } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';

import { CheckEmailPage } from './CheckEmailPage';
import { LoginPage } from './LoginPage';

vi.mock('@/features/auth/api', () => ({
  resendVerification: vi.fn(),
}));

const signIn = vi.fn();

vi.mock('@/features/auth/AuthContext', () => ({
  useAuth: () => ({ status: 'anonymous', signIn }),
}));

function renderLogin() {
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      { path: '/check-email', element: <CheckEmailPage /> },
    ],
    { initialEntries: ['/login'] },
  );

  render(<RouterProvider router={router} />);
}

async function fillAndSignIn(email: string, password: string) {
  fireEvent.change(await screen.findByLabelText('Email'), { target: { value: email } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginPage', () => {
  beforeEach(() => {
    signIn.mockReset();
    vi.mocked(resendVerification).mockReset();
  });

  it('keeps the way back for a forgotten password next to the password field', async () => {
    renderLogin();

    const forgot = await screen.findByRole('link', { name: 'Forgot your password?' });
    expect(forgot.getAttribute('href')).toBe('/forgot-password');
  });

  it('offers the confirmation link when the password was right but the address is not confirmed', async () => {
    signIn.mockRejectedValue(
      new ApiError(403, 'email_not_confirmed', 'Confirm your email address first.'),
    );
    vi.mocked(resendVerification).mockResolvedValue({ sent: true });
    renderLogin();

    await fillAndSignIn('sita@nepalhub.test', 'Wr0ngPassword!');

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Confirm your email address');

    fireEvent.click(screen.getByRole('button', { name: 'Email me the confirmation link again' }));

    expect(await screen.findByText('Confirm your email')).toBeTruthy();
    expect(vi.mocked(resendVerification)).toHaveBeenCalledWith({ email: 'sita@nepalhub.test' });
  });

  it('keeps an ordinary wrong password an ordinary refusal', async () => {
    signIn.mockRejectedValue(
      new ApiError(401, 'invalid_credentials', 'Email or password is incorrect.'),
    );
    renderLogin();

    await fillAndSignIn('sita@nepalhub.test', 'Wr0ngPassword!');

    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Email me the confirmation link again' })).toBeNull();
  });
});
