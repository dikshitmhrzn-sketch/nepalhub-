import { useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useMyPayments } from '@/features/payments/usePayments';
import {
  describePayment,
  paymentKindLabels,
  paymentStatusLabels,
} from '@/features/payments/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './PaymentsPage.module.css';

const PAGE_SIZE = 20;

/**
 * What one account has paid for: the plans and the tickets, in one list, because a refund
 * question is about the money rather than about which product it bought. The status is
 * spelled out rather than coloured, so "owed back" and "refunded" cannot be read as each
 * other at a glance.
 */
export function PaymentsPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const payments = useMyPayments({ page, pageSize: PAGE_SIZE });

  function goToPage(next: number) {
    const params = new URLSearchParams(searchParams);
    params.set('page', String(next));
    setSearchParams(params, { replace: true });
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Your payments</h1>
        <p className={styles.subtitle}>
          Plans and event tickets bought on Nepal Hub. A payment settles when the provider
          confirms it, which can take a moment after you come back from its pages.
        </p>
      </header>

      {payments.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading your payments…" />
      ) : null}

      {payments.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load your payments"
          description={getErrorMessage(payments.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={payments.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {payments.status === 'success' && payments.data.items.length === 0 ? (
        <StateMessage
          tone="empty"
          title="Nothing paid for yet"
          description="Anything you buy on this site — a plan for a listing, a ticket for an event — is listed here."
        />
      ) : null}

      {payments.status === 'success' && payments.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {payments.data.items.map((row) => (
              <li key={row.reference} className={styles.row}>
                <div className={styles.main}>
                  <p className={styles.description}>{row.description}</p>
                  <p className={styles.meta}>
                    {paymentKindLabels[row.kind]} · {row.createdAt.slice(0, 10)} ·{' '}
                    <span className={styles.reference}>{row.reference}</span>
                  </p>
                  <p className={styles.explanation}>{describePayment(row)}</p>
                </div>

                <div className={styles.amounts}>
                  <span className={styles.amount}>NPR {row.amountNpr.toLocaleString()}</span>
                  <span
                    className={`${styles.badge} ${
                      row.status === 'Succeeded'
                        ? styles.badgePaid
                        : row.status === 'Pending'
                          ? styles.badgePending
                          : styles.badgeAttention
                    }`}
                  >
                    {paymentStatusLabels[row.status]}
                  </span>
                </div>
              </li>
            ))}
          </ul>

          <Pagination
            page={payments.data.page}
            totalPages={payments.data.totalPages}
            totalCount={payments.data.totalCount}
            label="payments"
            onPageChange={goToPage}
          />
        </>
      ) : null}
    </Container>
  );
}
