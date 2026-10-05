import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useAuth } from '@/features/auth/AuthContext';
import { fileReport } from '@/features/reports/api';
import { ApiError } from '@/lib/apiClient';

import { ReportButton } from './ReportButton';

vi.mock('@/features/auth/AuthContext', () => ({ useAuth: vi.fn() }));
vi.mock('@/features/reports/api', () => ({ fileReport: vi.fn() }));

function signIn() {
  vi.mocked(useAuth).mockReturnValue({
    status: 'authenticated',
    user: {
      id: 'user-1',
      email: 'member@example.com',
      fullName: 'Member One',
      avatarUrl: null,
      isEmailConfirmed: true,
      level: 1,
      xp: 0,
      roles: ['user'],
    },
    isAdmin: false,
    isStaff: false,
    signIn: vi.fn(),
    signUp: vi.fn(),
    signOut: vi.fn(),
    reload: vi.fn(),
  });
}

function signInAsAnonymous() {
  vi.mocked(useAuth).mockReturnValue({
    status: 'anonymous',
    user: null,
    isAdmin: false,
    isStaff: false,
    signIn: vi.fn(),
    signUp: vi.fn(),
    signOut: vi.fn(),
    reload: vi.fn(),
  });
}

function fillAndSend() {
  fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Misleading' } });
  fireEvent.change(screen.getByLabelText('Note (optional)'), {
    target: { value: 'The price is not real' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Send report' }));
}

describe('ReportButton', () => {
  beforeEach(() => {
    signIn();
  });

  it('is not on the page at all for a signed-out visitor', () => {
    signInAsAnonymous();

    render(<ReportButton targetType="Job" targetId="job-1" />);

    expect(screen.queryByRole('button')).toBeNull();
  });

  it('opens the form, sends the listing as filled in, and confirms on screen', async () => {
  vi.mocked(fileReport).mockResolvedValue({
    id: 'report-1',
    targetType: 'Job',
    targetId: 'job-1',
    reason: 'Misleading',
    status: 'Open',
    createdAt: '2026-10-05T12:00:00Z',
  });

    render(<ReportButton targetType="Job" targetId="job-1" />);

    fireEvent.click(screen.getByRole('button', { name: 'Report this listing' }));

    fillAndSend();

    await waitFor(() =>
      expect(screen.getByText('Thanks — your report is with the moderators.')).toBeTruthy(),
    );

    expect(fileReport).toHaveBeenCalledWith({
      targetType: 'Job',
      targetId: 'job-1',
      reason: 'Misleading',
      note: 'The price is not real',
    });
  });

  it('keeps the form and explains an already-reported listing instead of failing silently', async () => {
    vi.mocked(fileReport).mockRejectedValue(
      new ApiError(409, 'already_reported', 'You already reported this.'),
    );

    render(<ReportButton targetType="Deal" targetId="deal-1" />);

    fireEvent.click(screen.getByRole('button', { name: 'Report this listing' }));
    fillAndSend();

    expect(
      await screen.findByText('You have already reported this listing. It is in the queue.'),
    ).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Send report' })).toBeTruthy();
  });

  it('says when the listing itself is gone rather than blaming the member', async () => {
    vi.mocked(fileReport).mockRejectedValue(
      new ApiError(404, 'target_not_found', 'Target not found.'),
    );

    render(<ReportButton targetType="Event" targetId="event-1" />);

    fireEvent.click(screen.getByRole('button', { name: 'Report this listing' }));
    fillAndSend();

    expect(
      await screen.findByText('This listing is no longer available to report.'),
    ).toBeTruthy();
  });

  it('lets the member back out of the form without sending anything', () => {
    render(<ReportButton targetType="Job" targetId="job-1" />);

    fireEvent.click(screen.getByRole('button', { name: 'Report this listing' }));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.getByRole('button', { name: 'Report this listing' })).toBeTruthy();
    expect(fileReport).not.toHaveBeenCalled();
  });
});
