import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { verifyPayment } from '@/features/payments/api';
import { paymentStatusLabels, type Payment } from '@/features/payments/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './PaymentReturnPage.module.css';

interface PaymentReturnPageProps {
  /**
   * Whether the provider sent the buyer here to say a payment failed. It is a claim by a
   * url, and the page asks the provider either way — a "failed" return that turns out to
   * be a settled payment says so rather than repeating the claim.
   */
  declaredFailure?: boolean;
}

type Phase =
  | { status: 'checking' }
  | { status: 'error'; error: ApiError }
  | { status: 'answered'; payment: Payment };

/**
 * Where the provider sends a buyer back.
 *
 * The signed payload, when one came back with the buyer, is offered once and then dropped:
 * a signature that has been refused once will be refused every time, so the retry asks by
 * reference alone, which is the question the settlement underneath answers anyway. Nothing
 * on this page settles a payment — the status check does, by talking to the provider.
 */
export function PaymentReturnPage({ declaredFailure = false }: PaymentReturnPageProps) {
  const { reference = '' } = useParams();
  const [searchParams] = useSearchParams();

  const carried = {
    data: searchParams.get('data'),
    signature: searchParams.get('signature'),
  };

  // Whether this attempt still offers the payload it arrived with. Flipped off by the
  // "ask again" path after a signature was refused.
  const [offerCarried, setOfferCarried] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const [phase, setPhase] = useState<Phase>({ status: 'checking' });

  useEffect(() => {
    let cancelled = false;

    setPhase({ status: 'checking' });

    void (async () => {
      try {
        const payment = await verifyPayment({
          reference,
          data: offerCarried ? carried.data : null,
          signature: offerCarried ? carried.signature : null,
        });

        if (!cancelled) {
          setPhase({ status: 'answered', payment });
        }
      } catch (error) {
        if (cancelled) {
          return;
        }

        setPhase({
          status: 'error',
          error:
            error instanceof ApiError
              ? error
              : new ApiError(0, 'unknown_error', 'Something went wrong while contacting the server.'),
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [reference, offerCarried, carried.data, carried.signature, attempt]);

  function askAgain() {
    setAttempt((value) => value + 1);
  }

  function askWithoutPayload() {
    setOfferCarried(false);
  }

  if (phase.status === 'checking') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Asking the provider…" />
      </Container>
    );
  }

  if (phase.status === 'error') {
    const { error } = phase;

    if (error.code === 'payment_signature_invalid' && offerCarried) {
      return (
        <Container as="section" className={styles.page}>
          <StateMessage
            tone="error"
            title="That reply could not be confirmed"
            description="Nothing has been settled on it. Ask the provider again by reference alone, which is the question that actually decides the payment."
            action={
              <Button variant="secondary" size="small" onClick={askWithoutPayload}>
                Ask the provider again
              </Button>
            }
          />
        </Container>
      );
    }

    if (error.status === 404) {
      return (
        <Container as="section" className={styles.page}>
          <StateMessage
            tone="error"
            title="No payment with that reference"
            description="It may belong to a different account, or it may have been written down wrongly."
            action={
              <Link to="/account/payments" className={styles.link}>
                See your payments
              </Link>
            }
          />
        </Container>
      );
    }

    if (error.status === 401 || error.code === 'session_invalid') {
      return (
        <Container as="section" className={styles.page}>
          <StateMessage
            tone="error"
            title="Please sign in again"
            description="The session that started this payment has ended, and a payment is read back only for the account that made it."
            action={
              <Link to="/login" className={styles.link}>
                Sign in
              </Link>
            }
          />
        </Container>
      );
    }

    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title="Could not reach the provider"
          description={getErrorMessage(error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={askAgain}>
              Try again
            </Button>
          }
        />
      </Container>
    );
  }

  const { payment } = phase;
  const amount = `NPR ${payment.amountNpr.toLocaleString()}`;

  const outcome = (() => {
    switch (payment.status) {
      case 'Succeeded':
        return {
          tone: 'info' as const,
          title: declaredFailure
            ? 'The provider says this payment went through'
            : 'Payment confirmed',
          description: `${payment.description} — ${amount}. ${paymentStatusLabels[payment.status]}.`,
        };
      case 'Pending':
        return {
          tone: 'info' as const,
          title: 'Still waiting for the provider',
          description:
            'Nothing has been settled yet. The provider answers when it has finished, and asking again is the same question rather than a second payment.',
        };
      case 'Failed':
        return {
          tone: 'error' as const,
          title: 'The payment did not go through',
          description: `${payment.description} — ${amount}. Nothing was taken for this one.`,
        };
      case 'RefundDue':
        return {
          tone: 'info' as const,
          title: 'This payment is owed back',
          description: 'The money has been taken and is waiting to be returned.',
        };
      default:
        return {
          tone: 'info' as const,
          title: 'This payment has been refunded',
          description: 'The money has gone back to whoever paid.',
        };
    }
  })();

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <p className={styles.reference}>Reference {payment.reference}</p>

        <StateMessage
          tone={outcome.tone}
          title={outcome.title}
          description={outcome.description}
          action={
            payment.status === 'Pending' ? (
              <Button variant="secondary" size="small" onClick={askAgain}>
                Check again
              </Button>
            ) : undefined
          }
        />

        <div className={styles.actions}>
          <Link to="/account/payments" className={styles.link}>
            See your payments
          </Link>
          <Link to="/" className={styles.link}>
            Back to the site
          </Link>
        </div>

        <p className={styles.note}>
          A return page on its own settles nothing: the payment is confirmed by asking the
          provider directly, so a hand-edited address here cannot decide what happened.
        </p>
      </div>
    </Container>
  );
}
