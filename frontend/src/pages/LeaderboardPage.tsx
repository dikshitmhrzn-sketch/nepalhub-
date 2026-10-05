import { useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useLeaderboard } from '@/features/leaderboard/useLeaderboard';
import { leaderboardPeriods, type LeaderboardPeriod } from '@/features/leaderboard/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './LeaderboardPage.module.css';

const periodTabs: { period: LeaderboardPeriod; label: string }[] = [
  { period: 'all', label: 'All time' },
  { period: 'today', label: 'Today' },
  { period: 'week', label: 'This week' },
];

/**
 * The XP standings. The URL owns the period, so a window can be shared or reloaded, and
 * an address with a window this board does not have falls back to all time rather than
 * being refused — the board's own answer to a typo is the full ranking.
 *
 * The XP shown is the XP the rank was decided by: lifetime on all time, the window's own
 * on the other two. A reader should never have to reconcile a number with an order.
 */
export function LeaderboardPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const requested = searchParams.get('period');
  const period = leaderboardPeriods.find((value) => value === requested) ?? 'all';

  const board = useLeaderboard(period);

  function selectPeriod(next: LeaderboardPeriod) {
    const params = new URLSearchParams(searchParams);

    if (next === 'all') {
      params.delete('period');
    } else {
      params.set('period', next);
    }

    setSearchParams(params, { replace: true });
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Leaderboard</h1>
        <p className={styles.subtitle}>
          The members at the top of the XP standings. Each window ranks a different stretch —
          everything ever, the Nepal day, or the current week — and the XP beside each name is
          what the rank was decided by.
        </p>
      </header>

      <div className={styles.tabs} role="group" aria-label="Time window">
        {periodTabs.map((tab) => (
          <button
            key={tab.period}
            type="button"
            className={tab.period === period ? `${styles.tab} ${styles.tabActive}` : styles.tab}
            aria-pressed={tab.period === period}
            onClick={() => selectPeriod(tab.period)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {board.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading the leaderboard" />
      ) : null}

      {board.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the leaderboard"
          description={getErrorMessage(board.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={board.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {board.status === 'success' && board.data.items.length === 0 ? (
        <StateMessage
          tone="empty"
          title="Nobody is on this board yet"
          description="Finish the daily quiz to earn XP, and your name will appear here."
        />
      ) : null}

      {board.status === 'success' && board.data.items.length > 0 ? (
        <>
          <ol className={styles.board}>
            {board.data.items.map((entry) => (
              <li
                key={entry.rank}
                className={entry.rank <= 3 ? `${styles.row} ${styles.rowTop}` : styles.row}
              >
                <span className={styles.rank} aria-label={`Rank ${entry.rank}`}>
                  {entry.rank}
                </span>
                <span className={styles.name}>{entry.displayName}</span>
                <span className={styles.level}>Level {entry.level}</span>
                <span className={styles.xp}>{entry.xp.toLocaleString()} XP</span>
              </li>
            ))}
          </ol>

          <p className={styles.note}>
            {board.data.totalCount} members shown. Weeks run from Saturday to Friday, Nepal time.
          </p>
        </>
      ) : null}
    </Container>
  );
}
