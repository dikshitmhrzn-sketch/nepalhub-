import { useState } from 'react';
import { useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { checkPaymentStatus } from '@/features/payments/api';
import { useAdminPayments } from '@/features/payments/usePayments';
import {
  describePayment,
  paymentFilters,
  paymentFilterLabels,
  paymentKindLabels,
  paymentStatusLabels,
  type PaymentFilter,
} from '@/features/payments/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './PaymentsAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * The money, from the back office: which payments exist, what they stand at, and the way to
 * ask the provider by hand. The second half is the one that matters — eSewa reports nothing
 * back on its own, so a settlement nobody came back for, or a refund taken in the provider's
 * own portal, is only ever found by asking.
 */
export function PaymentsAdminPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const requested = searchParams.get('status');
  const filter = paymentFilters.find((value) => value === requested) ?? 'all';

  const payments = useAdminPayments({ page, pageSize: PAGE_SIZE, filter });

  const [checking, setChecking] = useState<string | null>(null);
  const [rowNote, setRowNote] = useState<{ reference: string; message: string } | null>(null);

  function selectWindow(next: PaymentFilter) {
    const params = new URLSearchParams(searchParams);
    if (next === 'all') params.delete('status');
    else params.set('status', next);
    params.delete('page');
    setSearchParams(params, { replace: true });
  }

  function goToPage(next: number) {
    const params = new URLSearchParams(searchParams);
    params.set('page', String(next));
    setSearchParams(params, { replace: true });
  }

  async function handleCheck(reference: string) {
    setChecking(reference);
    setRowNote(null);

    try {
      await checkPaymentStatus(reference);
      await payments.reload();
    } catch (error) {
      setRowNote({
        reference,
        message: getErrorMessage(error, 'The provider could not be asked about that payment.'),
      });
    } finally {
      setChecking(null);
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Payments</h1>
        <p className={styles.subtitle}>
          Everything bought on the site, with the buyer it belongs to. Checking a payment asks
          the provider what became of it, which is how a payment nobody came back for is
          settled and how a refund taken on the provider&rsquo;s side is noticed.
        </p>
      </header>

      <div className={styles.tabs} role="group" aria-label="Payment window">
        {paymentFilters.map((value) => (
          <button
            key={value}
            type="button"
            className={value === filter ? `${styles.tab} ${styles.tabActive}` : styles.tab}
            aria-pressed={value === filter}
            onClick={() => selectWindow(value)}
          >
            {paymentFilterLabels[value]}
          </button>
        ))}
      </div>

      {payments.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading the payments…" />
      ) : null}

      {payments.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the payments"
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
          title="No payments here"
          description="Nothing has been bought in this window yet."
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
                    {row.buyerName}
                    {row.buyerEmail ? ` · ${row.buyerEmail}` : ''}
                  </p>
                  <p className={styles.meta}>
                    {paymentKindLabels[row.kind]} · {row.createdAt.slice(0, 10)} ·{' '}
                    <span className={styles.reference}>{row.reference}</span>
                  </p>
                  <p className={styles.explanation}>{describePayment(row)}</p>

                  {rowNote?.reference === row.reference ? (
                    <p className={styles.rowError} role="alert">
                      {rowNote.message}
                    </p>
                  ) : null}
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

                  <Button
                    type="button"
                    variant="secondary"
                    size="small"
                    disabled={checking !== null}
                    onClick={() => void handleCheck(row.reference)}
                  >
                    {checking === row.reference ? 'Asking…' : 'Check with provider'}
                  </Button>
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
