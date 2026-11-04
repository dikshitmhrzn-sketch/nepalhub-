import { useState } from 'react';

import { Button } from '@/components/ui/Button';
import { reviewSubmission } from '@/features/submissions/api';
import {
  submissionKindLabels,
  submissionStatusLabels,
  type AdminSubmissionRow,
} from '@/features/submissions/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage, type FieldError } from '@/lib/apiErrors';

import styles from './SubmissionQueueCard.module.css';

interface SubmissionQueueCardProps {
  row: AdminSubmissionRow;
  onChanged: () => void;
  onEdit: (row: AdminSubmissionRow) => void;
}

/**
 * One member submission in the staff queue, and the three ways out of it: correct the draft
 * and approve it, turn it down with a note, or leave it to another moderator.
 *
 * Approval publishes the draft when it satisfies the publish rules, so a draft that has
 * gained a blocker since it was sent answers 409 with those blockers — they are listed on
 * this card rather than left to be rediscovered, because the author cannot see them until
 * somebody tells them. A submission another moderator decided answers 409 too, which
 * reloads the queue rather than pretending the second click landed.
 */
export function SubmissionQueueCard({ row, onChanged, onEdit }: SubmissionQueueCardProps) {
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState<'approve' | 'reject' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [blockers, setBlockers] = useState<string[]>([]);

  const isOpen = row.status === 'Pending';

  async function review(decision: 'approve' | 'reject') {
    setBusy(decision);
    setError(null);
    setBlockers([]);

    try {
      await reviewSubmission(row.id, decision, note.trim() || null);
      onChanged();
    } catch (reviewError) {
      setBusy(null);

      if (reviewError instanceof ApiError && reviewError.code === 'not_publishable') {
        setError(
          'This draft does not satisfy the publish rules yet, so it cannot be approved as it ' +
            'stands. Open it with Edit and fix the items listed below, or turn it down and tell ' +
            'the author what to change.',
        );

        const details = Array.isArray(reviewError.details) ? (reviewError.details as FieldError[]) : [];
        setBlockers(details.map((detail) => detail.message).filter(Boolean));
      } else if (reviewError instanceof ApiError && reviewError.code === 'submission_closed') {
        setError('Another moderator has already decided this submission.');
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
          {submissionStatusLabels[row.status]}
        </span>
        <p className={styles.filed}>
          {submissionKindLabels[row.kind]} · sent by {row.submitterName}
          {row.submitterEmail ? ` · ${row.submitterEmail}` : ''} ·{' '}
          {new Date(row.createdAt).toLocaleDateString()}
        </p>
      </div>

      <p className={styles.target}>{row.title}</p>

      {row.note ? <p className={styles.note}>&ldquo;{row.note}&rdquo;</p> : null}

      {!isOpen ? (
        <p className={styles.decision}>
          {submissionStatusLabels[row.status]}
          {row.reviewedAt ? ` · ${new Date(row.reviewedAt).toLocaleDateString()}` : ''}
          {row.reviewNote ? ` — “${row.reviewNote}”` : ''}
        </p>
      ) : null}

      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : null}

      {blockers.length > 0 ? (
        <ul className={styles.blockers}>
          {blockers.map((blocker) => (
            <li key={blocker}>{blocker}</li>
          ))}
        </ul>
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
              placeholder="What was checked, or what the author should change."
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
            <Button
              type="button"
              size="small"
              variant="ghost"
              disabled={busy !== null}
              onClick={() => onEdit(row)}
            >
              Edit
            </Button>
          </div>
        </div>
      ) : null}
    </li>
  );
}
