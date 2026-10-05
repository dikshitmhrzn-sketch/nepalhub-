import { useSearchParams } from 'react-router';

import { SubscriptionRequestCard } from '@/components/subscriptions/SubscriptionRequestCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  useSubscriptionRequests,
} from '@/features/subscriptions/useSubscriptions';
import {
  subscriptionRequestFilters,
  subscriptionRequestFilterLabels,
  type SubscriptionRequestFilter,
} from '@/features/subscriptions/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './SubscriptionRequestsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * The queue of owners asking for a plan. Administrators only, because until a payment
 * provider exists, approving a paid request is the moment the placement is granted —
 * the same decision the direct-grant screen already reserves for administrators.
 *
 * The URL owns the window and the page: an address naming a window this queue does not
 * have falls back to all, waiting work first, rather than being refused.
 */
export function SubscriptionRequestsAdminPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const requested = searchParams.get('status');
  const filter =
    subscriptionRequestFilters.find((value) => value === requested) ?? 'all';

  const queue = useSubscriptionRequests({ page, pageSize: PAGE_SIZE, filter });

  function selectWindow(next: SubscriptionRequestFilter) {
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
        <h1 className={styles.title}>Plan requests</h1>
        <p className={styles.subtitle}>
          Owners asking for a plan for their listing, newest first with the waiting work at the
          top. Approving opens or rolls the window in the same save and writes your decision to
          the audit log.
        </p>
      </header>

      <div className={styles.tabs} role="group" aria-label="Request window">
        {subscriptionRequestFilters.map((value) => (
          <button
            key={value}
            type="button"
            className={
              value === filter ? `${styles.tab} ${styles.tabActive}` : styles.tab
            }
            aria-pressed={value === filter}
            onClick={() => selectWindow(value)}
          >
            {subscriptionRequestFilterLabels[value]}
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
          title="No requests here"
          description="Nothing matches this window. Try another one, or check back when an owner asks for a plan."
        />
      ) : null}

      {queue.status === 'success' && queue.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {queue.data.items.map((row) => (
              <SubscriptionRequestCard
                key={row.id}
                row={row}
                onChanged={queue.reload}
              />
            ))}
          </ul>

          <Pagination
            page={queue.data.page}
            totalPages={queue.data.totalPages}
            totalCount={queue.data.totalCount}
            label="requests"
            onPageChange={goToPage}
          />
        </>
      ) : null}
    </Container>
  );
}
