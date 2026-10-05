import { useSearchParams } from 'react-router';

import { ReportQueueCard } from '@/components/reports/ReportQueueCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useReportQueue } from '@/features/reports/useReports';
import {
  reportQueueFilterLabels,
  reportQueueFilters,
  type ReportQueueFilter,
} from '@/features/reports/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ReportsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * The moderation queue. The URL owns the window and the page, so a moderator can share
 * "everything open" or reload without losing their place, and an address naming a window
 * this queue does not have falls back to all rather than being refused — the queue's own
 * answer to a typo is the full list, open work first.
 */
export function ReportsAdminPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const requested = searchParams.get('status');
  const status = reportQueueFilters.find((value) => value === requested) ?? 'all';

  const queue = useReportQueue({ page, pageSize: PAGE_SIZE, status });

  function selectWindow(next: ReportQueueFilter) {
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
        <h1 className={styles.title}>Report queue</h1>
        <p className={styles.subtitle}>
          What members have complained about, newest first with the open work at the top. Closing a
          report records your decision and your note in the audit log.
        </p>
      </header>

      <div className={styles.tabs} role="group" aria-label="Report window">
        {reportQueueFilters.map((value) => (
          <button
            key={value}
            type="button"
            className={value === status ? `${styles.tab} ${styles.tabActive}` : styles.tab}
            aria-pressed={value === status}
            onClick={() => selectWindow(value)}
          >
            {reportQueueFilterLabels[value]}
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
          title="No reports here"
          description="Nothing matches this window. Try another one, or check back when members start reporting listings."
        />
      ) : null}

      {queue.status === 'success' && queue.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {queue.data.items.map((row) => (
              <ReportQueueCard key={row.id} row={row} onChanged={queue.reload} />
            ))}
          </ul>

          <Pagination
            page={queue.data.page}
            totalPages={queue.data.totalPages}
            totalCount={queue.data.totalCount}
            label="reports"
            onPageChange={goToPage}
          />
        </>
      ) : null}
    </Container>
  );
}
