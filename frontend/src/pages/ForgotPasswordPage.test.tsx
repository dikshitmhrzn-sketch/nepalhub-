import { fireEvent, render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { forgotPassword } from '@/features/auth/api';
import { ApiError } from '@/lib/apiClient';

import { CheckEmailPage } from './CheckEmailPage';
import { ForgotPasswordPage } from './ForgotPasswordPage';

vi.mock('@/features/auth/api', () => ({
  forgotPassword: vi.fn(),
  resendVerification: vi.fn(),
}));

function renderForgot() {
  const router = createMemoryRouter(
    [
      { path: '/forgot-password', element: <ForgotPasswordPage /> },
      { path: '/check-email', element: <CheckEmailPage /> },
    ],
    { initialEntries: ['/forgot-password'] },
  );

  render(<RouterProvider router={router} />);
}

async function submitEmail(value: string) {
  fireEvent.change(await screen.findByLabelText('Email'), { target: { value } });
  fireEvent.click(screen.getByRole('button', { name: 'Send a reset link' }));
}

describe('ForgotPasswordPage', () => {
  beforeEach(() => {
    vi.mocked(forgotPassword).mockReset();
  });

  it('sends the ask and waits on the same screen a known address would get', async () => {
    vi.mocked(forgotPassword).mockResolvedValue({ sent: true });
    renderForgot();

    await submitEmail('sita@nepalhub.test');

    expect(await screen.findByText('Check your email')).toBeTruthy();
    expect(screen.getByText(/sita@nepalhub\.test/u)).toBeTruthy();
    expect(vi.mocked(forgotPassword)).toHaveBeenCalledWith({ email: 'sita@nepalhub.test' });
  });

  it('shows a refusal above the form instead of pretending it sent', async () => {
    vi.mocked(forgotPassword).mockRejectedValue(
      new ApiError(429, 'rate_limited', 'Too many requests.'),
    );
    renderForgot();

    await submitEmail('sita@nepalhub.test');

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Too many attempts');
    expect(screen.getByRole('button', { name: 'Send a reset link' })).toBeTruthy();
  });
});
