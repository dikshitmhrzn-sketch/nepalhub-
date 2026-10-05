import { useSearchParams } from 'react-router';

import { ClaimQueueCard } from '@/components/claims/ClaimQueueCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useClaims } from '@/features/ownership/useClaims';
import { claimFilters, claimFilterLabels, type ClaimFilter } from '@/features/ownership/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ClaimsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * The ownership queue. The URL owns the window and the page, so a moderator can share
 * "everything open" or reload without losing their place, and an address naming a window
 * this queue does not have falls back to all rather than being refused — this queue's own
 * answer to a typo is the full list, open work first.
 */
export function ClaimsAdminPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const requested = searchParams.get('status');
  const filter = claimFilters.find((value) => value === requested) ?? 'all';

  const queue = useClaims({ page, pageSize: PAGE_SIZE, filter });

  function selectWindow(next: ClaimFilter) {
    const params = new URLSearchParams(searchParams);

    if (next === 'all') {
      params.delete('status');
    } else {
      params.set('status', next);
    }

    params.delete('page');
    setSearchParams(params, { replace: true });
  }

  function goToPage(next: number) {
    const params = new URLSearchParams(searchParams);
    params.set('page', String(next));
    setSearchParams(params, { replace: true });
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Ownership claims</h1>
        <p className={styles.subtitle}>
          Who says a listing is theirs, newest first with the open work at the top. Approving a
          claim moves the listing to the claimant, settles the rivals, and writes your decision
          to the audit log.
        </p>
      </header>

      <div className={styles.tabs} role="group" aria-label="Claim window">
        {claimFilters.map((value) => (
          <button
            key={value}
            type="button"
            className={value === filter ? `${styles.tab} ${styles.tabActive}` : styles.tab}
            aria-pressed={value === filter}
            onClick={() => selectWindow(value)}
          >
            {claimFilterLabels[value]}
          </button>
        ))}
      </div>

      {queue.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading the queue" />
      ) : null}

      {queue.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the queue"
          description={getErrorMessage(queue.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={queue.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {queue.status === 'success' && queue.data.items.length === 0 ? (
        <StateMessage
          tone="empty"
          title="No claims here"
          description="Nothing matches this window. Try another one, or check back when a reader claims a listing."
        />
      ) : null}

      {queue.status === 'success' && queue.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {queue.data.items.map((row) => (
              <ClaimQueueCard key={row.id} row={row} onChanged={queue.reload} />
            ))}
          </ul>

          <Pagination
            page={queue.data.page}
            totalPages={queue.data.totalPages}
            totalCount={queue.data.totalCount}
            label="claims"
            onPageChange={goToPage}
          />
        </>
      ) : null}
    </Container>
  );
}
