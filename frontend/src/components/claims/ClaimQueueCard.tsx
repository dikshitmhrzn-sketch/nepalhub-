import { useState } from 'react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { reviewClaim } from '@/features/ownership/api';
import { claimStatusLabels, type AdminClaimRow } from '@/features/ownership/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ClaimQueueCard.module.css';

interface ClaimQueueCardProps {
  row: AdminClaimRow;
  onChanged: () => void;
}

/**
 * One claim in the staff queue, and the two ways out of it.
 *
 * A decided claim keeps showing: the note and who decided it are the record, and hiding
 * the row the moment it closes would take that record off the screen. A claim another
 * moderator decided in the meantime answers 409 — which reloads the queue rather than
 * pretending the second click landed, because the row has moved.
 */
export function ClaimQueueCard({ row, onChanged }: ClaimQueueCardProps) {
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState<'approve' | 'reject' | null>(null);
  const [error, setError] = useState<string | null>(null);

  const isOpen = row.status === 'Pending';

  async function review(decision: 'approve' | 'reject') {
    setBusy(decision);
    setError(null);

    try {
      await reviewClaim(row.id, decision, note.trim() || null);
      onChanged();
    } catch (reviewError) {
      setBusy(null);

      if (reviewError instanceof ApiError && reviewError.code === 'claim_closed') {
        setError('Another moderator has already decided this claim.');
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
          {claimStatusLabels[row.status]}
        </span>
        <p className={styles.filed}>
          Claimed by {row.claimantName}
          {row.claimantEmail ? ` · ${row.claimantEmail}` : ''} ·{' '}
          {new Date(row.createdAt).toLocaleDateString()}
        </p>
      </div>

      <p className={styles.target}>
        <Link to={`/businesses/${encodeURIComponent(row.slug)}`} className={styles.targetLink}>
          {row.businessName}
        </Link>
      </p>

      {row.evidence ? <p className={styles.note}>&ldquo;{row.evidence}&rdquo;</p> : null}

      {!isOpen ? (
        <p className={styles.decision}>
          {claimStatusLabels[row.status]}
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
