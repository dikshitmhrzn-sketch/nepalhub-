import { useState } from 'react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { reviewSubscriptionRequest } from '@/features/subscriptions/api';
import {
  planTierLabels,
  subscriptionRequestFilterLabels,
  type AdminSubscriptionRequestRow,
} from '@/features/subscriptions/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './SubscriptionRequestCard.module.css';

interface SubscriptionRequestCardProps {
  row: AdminSubscriptionRequestRow;
  onChanged: () => void;
}

/**
 * One owner's request for a plan, and the two ways out of the queue.
 *
 * Approving a paid request is, until a payment provider exists, the moment the placement is
 * granted — the same decision the direct-grant screen reserves for administrators — so the
 * note here is the closest thing to a receipt this phase has. A request another admin
 * decided meanwhile answers 409, which reloads the queue rather than faking the second click.
 */
export function SubscriptionRequestCard({ row, onChanged }: SubscriptionRequestCardProps) {
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState<'approve' | 'reject' | null>(null);
  const [error, setError] = useState<string | null>(null);

  const isOpen = row.status === 'Pending';

  async function review(decision: 'approve' | 'reject') {
    setBusy(decision);
    setError(null);

    try {
      await reviewSubscriptionRequest(row.id, decision, note.trim() || null);
      onChanged();
    } catch (reviewError) {
      setBusy(null);

      if (reviewError instanceof ApiError && reviewError.code === 'request_closed') {
        setError('Another administrator has already decided this request.');
        onChanged();
      } else {
        setError(getErrorMessage(reviewError, 'Could not save the decision. Please try again.'));
      }
    }
  }

  return (
    <li className={isOpen ? styles.card : `${styles.card} ${styles.cardClosed}`}>
      <div className={styles.head}>
        <span
          className={
            isOpen ? `${styles.badge} ${styles.badgeOpen}` : `${styles.badge} ${styles.badgeClosed}`
          }
        >
          {subscriptionRequestFilterLabels[row.status]}
        </span>
        <p className={styles.tier}>{planTierLabels[row.tier]} plan</p>
        <p className={styles.filed}>
          Asked by {row.requesterName}
          {row.requesterEmail ? ` · ${row.requesterEmail}` : ''} ·{' '}
          {new Date(row.createdAt).toLocaleDateString()}
        </p>
      </div>

      <p className={styles.target}>
        <Link to={`/businesses/${encodeURIComponent(row.slug)}`} className={styles.targetLink}>
          {row.businessName}
        </Link>
      </p>

      {row.note ? <p className={styles.note}>&ldquo;{row.note}&rdquo;</p> : null}

      {!isOpen ? (
        <p className={styles.decision}>
          {subscriptionRequestFilterLabels[row.status]}
          {row.reviewedAt ? ` · ${new Date(row.reviewedAt).toLocaleDateString()}` : ''}
          {row.reviewNote ? ` — “${row.reviewNote}”` : ''}
        </p>
      ) : null}

      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : null}

      {isOpen ? (
        <div className={styles.actions}>
          <label className={styles.noteField}>
            <span className={styles.noteLabel}>Decision note (optional)</span>
            <input
              className={styles.noteInput}
              type="text"
              maxLength={500}
              value={note}
              disabled={busy !== null}
              placeholder="What was decided, in a sentence."
              onChange={(event) => setNote(event.target.value)}
            />
          </label>

          <div className={styles.buttons}>
            <Button
              type="button"
              size="small"
              variant="secondary"
              disabled={busy !== null}
              onClick={() => void review('approve')}
            >
              {busy === 'approve' ? 'Saving…' : 'Approve'}
            </Button>
            <Button
              type="button"
              size="small"
              variant="ghost"
              disabled={busy !== null}
              onClick={() => void review('reject')}
            >
              {busy === 'reject' ? 'Saving…' : 'Reject'}
            </Button>
          </div>
        </div>
      ) : null}
    </li>
  );
}
