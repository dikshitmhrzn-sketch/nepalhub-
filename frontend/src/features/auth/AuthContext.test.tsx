import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import * as authApi from '@/features/auth/api';

import { AuthProvider, useAuth } from './AuthContext';

vi.mock('@/features/auth/api', () => ({
  getSession: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}));

function Probe() {
  const { status, signUp } = useAuth();

  return (
    <div>
      <p data-testid="status">{status}</p>
      <button
        type="button"
        onClick={() =>
          void signUp({
            email: 'sita@nepalhub.test',
            password: 'Wr0ngPassword!',
            fullName: 'Sita Sharma',
            phoneNumber: null,
          })
        }
      >
        join
      </button>
    </div>
  );
}

function user(isEmailConfirmed: boolean) {
  return {
    id: '3f1d2a4c-0000-4000-8000-000000000001',
    email: 'sita@nepalhub.test',
    fullName: 'Sita Sharma',
    avatarUrl: null,
    isEmailConfirmed,
    level: 1,
    xp: 0,
    roles: [],
  };
}

describe('AuthContext', () => {
  beforeEach(() => {
    vi.mocked(authApi.getSession).mockReset();
    vi.mocked(authApi.register).mockReset();
    vi.mocked(authApi.getSession).mockResolvedValue({ authenticated: false, user: null });
  });

  it('leaves a freshly created, unconfirmed account signed out', async () => {
    vi.mocked(authApi.register).mockResolvedValue(user(false));

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect((await screen.findByTestId('status')).textContent).toBe('anonymous');

    fireEvent.click(screen.getByRole('button', { name: 'join' }));

    // The server minted no cookie, so there is no session to hold: an optimistic
    // "authenticated" here would collapse the first time anything asked the server.
    await waitFor(() => expect(vi.mocked(authApi.register)).toHaveBeenCalledTimes(1));
    expect((await screen.findByTestId('status')).textContent).toBe('anonymous');
  });

  it('signs the visitor straight in when the address comes back confirmed', async () => {
    vi.mocked(authApi.register).mockResolvedValue(user(true));

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    await screen.findByTestId('status');
    fireEvent.click(screen.getByRole('button', { name: 'join' }));

    await waitFor(() =>
      expect((screen.getByTestId('status') as HTMLParagraphElement).textContent).toBe(
        'authenticated',
      ),
    );
  });
});
