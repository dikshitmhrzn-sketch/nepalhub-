import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { listLeaderboard } from '@/features/leaderboard/api';
import type { LeaderboardEntry, PagedResult } from '@/features/leaderboard/types';
import { ApiError } from '@/lib/apiClient';

import { LeaderboardPage } from './LeaderboardPage';

vi.mock('@/features/leaderboard/api', () => ({ listLeaderboard: vi.fn() }));

const board: PagedResult<LeaderboardEntry> = {
  items: [
    { rank: 1, displayName: 'Asha Gurung', level: 9, xp: 9210 },
    { rank: 2, displayName: 'Bikram Thapa', level: 7, xp: 7440 },
    { rank: 3, displayName: 'Chhaya Rai', level: 5, xp: 5003 },
    { rank: 4, displayName: 'Dipak Shah', level: 4, xp: 3210 },
  ],
  page: 1,
  pageSize: 100,
  totalCount: 4,
  totalPages: 1,
  hasPrevious: false,
  hasNext: false,
};

function renderPage(initialUrl = '/leaderboard') {
  const router = createMemoryRouter([{ path: '/leaderboard', element: <LeaderboardPage /> }], {
    initialEntries: [initialUrl],
  });

  render(<RouterProvider router={router} />);
}

describe('LeaderboardPage', () => {
  it('lists the board in rank order, with the window the URL asked for', async () => {
    vi.mocked(listLeaderboard).mockResolvedValue(board);
    renderPage('/leaderboard?period=week');

    await waitFor(() => expect(screen.getByRole('heading', { level: 1 })).toBeTruthy());

    expect(listLeaderboard).toHaveBeenCalledWith('week', expect.anything());

    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(4);
    expect(rows[0].textContent).toContain('Asha Gurung');
    expect(rows[0].textContent).toContain('Level 9');
    expect(rows[3].textContent).toContain('Dipak Shah');

    // The window is a pressed choice rather than a link the reader has to guess at.
    expect(screen.getByRole('button', { name: 'This week' }).getAttribute('aria-pressed')).toBe(
      'true',
    );
    expect(screen.getByRole('button', { name: 'All time' }).getAttribute('aria-pressed')).toBe(
      'false',
    );
  });

  it('asks the API for the window a tab selects', async () => {
    vi.mocked(listLeaderboard).mockResolvedValue(board);
    renderPage();

    await waitFor(() => expect(screen.getByRole('button', { name: 'Today' })).toBeTruthy());

    fireEvent.click(screen.getByRole('button', { name: 'Today' }));

    await waitFor(() => expect(listLeaderboard).toHaveBeenCalledTimes(2));
    expect(listLeaderboard).toHaveBeenLastCalledWith('today', expect.anything());
  });

  it('falls back to all time when the URL names a window this board does not have', async () => {
    vi.mocked(listLeaderboard).mockResolvedValue(board);
    renderPage('/leaderboard?period=bananas');

    await waitFor(() => expect(screen.getByRole('heading', { level: 1 })).toBeTruthy());

    expect(listLeaderboard).toHaveBeenCalledWith('all', expect.anything());
  });

  it('says so plainly when nobody has earned any XP yet', async () => {
    vi.mocked(listLeaderboard).mockResolvedValue({ ...board, items: [], totalCount: 0 });
    renderPage();

    await waitFor(() => expect(screen.getByText('Nobody is on this board yet')).toBeTruthy());
    expect(screen.queryByRole('listitem')).toBeNull();
  });

  it('offers a retry when the board cannot be loaded', async () => {
    vi.mocked(listLeaderboard)
      .mockRejectedValueOnce(new ApiError(0, 'network_error', 'Could not reach the server.'))
      .mockResolvedValueOnce(board);

    renderPage();

    await waitFor(() => expect(screen.getByText('Could not load the leaderboard')).toBeTruthy());
    expect(screen.getByText('Could not reach the server.')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(screen.getByText('Asha Gurung')).toBeTruthy());
    expect(listLeaderboard).toHaveBeenCalledTimes(2);
  });
});
