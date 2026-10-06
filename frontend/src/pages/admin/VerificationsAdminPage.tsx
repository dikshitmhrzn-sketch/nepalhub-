import { useSearchParams } from 'react-router';

import { VerificationQueueCard } from '@/components/verifications/VerificationQueueCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useVerifications } from '@/features/ownership/useVerifications';
import {
  verificationFilters,
  verificationFilterLabels,
  type VerificationFilter,
} from '@/features/ownership/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './VerificationsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * The verification queue: listings an owner registered themselves, newest first with the
 * open work at the top. Approving is the moment a draft gains the badge and the public
 * address, so the button says as much, and a listing that fails the publish rules answers
 * on its own row rather than leaving the request to be re-opened by hand.
 */
export function VerificationsAdminPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const requested = searchParams.get('status');
  const filter = verificationFilters.find((value) => value === requested) ?? 'all';

  const queue = useVerifications({ page, pageSize: PAGE_SIZE, filter });

  function selectWindow(next: VerificationFilter) {
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
        <h1 className={styles.title}>Verification requests</h1>
        <p className={styles.subtitle}>
          Listings their owners registered, newest first with the open work at the top.
          Approving checks the publish rules, publishes the listing, gives the owner the
          business role and writes your decision to the audit log — all as one statement.
        </p>
      </header>

      <div className={styles.tabs} role="group" aria-label="Verification window">
        {verificationFilters.map((value) => (
          <button
            key={value}
            type="button"
            className={value === filter ? `${styles.tab} ${styles.tabActive}` : styles.tab}
            aria-pressed={value === filter}
            onClick={() => selectWindow(value)}
          >
            {verificationFilterLabels[value]}
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
          description="Nothing matches this window. Try another one, or check back when an owner asks for a listing to be checked."
        />
      ) : null}

      {queue.status === 'success' && queue.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {queue.data.items.map((row) => (
              <VerificationQueueCard key={row.id} row={row} onChanged={queue.reload} />
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
