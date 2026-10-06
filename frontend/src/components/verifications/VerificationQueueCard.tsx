import { useState } from 'react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { reviewVerification } from '@/features/ownership/api';
import { verificationStatusLabels, type AdminVerificationRow } from '@/features/ownership/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './VerificationQueueCard.module.css';

interface VerificationQueueCardProps {
  row: AdminVerificationRow;
  onChanged: () => void;
}

/**
 * One verification request in the staff queue, and the two ways out of it.
 *
 * Approval publishes: the listing gains the badge and the public address only when it
 * satisfies the publish rules, and a listing that has gained a blocker since it was
 * submitted answers 409 — so the reason lands on this card rather than the row silently
 * staying open with nobody told why. A request another moderator decided in the meantime
 * answers 409 too, which reloads the queue rather than pretending the second click landed.
 */
export function VerificationQueueCard({ row, onChanged }: VerificationQueueCardProps) {
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState<'approve' | 'reject' | null>(null);
  const [error, setError] = useState<string | null>(null);

  const isOpen = row.status === 'Pending';

  async function review(decision: 'approve' | 'reject') {
    setBusy(decision);
    setError(null);

    try {
      await reviewVerification(row.id, decision, note.trim() || null);
      onChanged();
    } catch (reviewError) {
      setBusy(null);

      if (reviewError instanceof ApiError && reviewError.code === 'not_publishable') {
        setError(
          'This listing does not satisfy the publish rules yet — it needs a phone number and ' +
            'a description of at least sixty characters. The owner can add them from their ' +
            'listing editor, and you can ask them to through the note field.',
        );
      } else if (reviewError instanceof ApiError && reviewError.code === 'verification_closed') {
        setError('Another moderator has already decided this request.');
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
          {verificationStatusLabels[row.status]}
        </span>
        <p className={styles.filed}>
          Registered by {row.ownerName}
          {row.ownerEmail ? ` · ${row.ownerEmail}` : ''} ·{' '}
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
          {verificationStatusLabels[row.status]}
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
              placeholder="What was checked, or what the owner should fix."
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
              {busy === 'approve' ? 'Saving…' : 'Approve and publish'}
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
