import { useState } from 'react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { closeReport } from '@/features/reports/api';
import { formatFiledAt, statusLabel } from '@/features/reports/format';
import {
  reportReasonLabels,
  reportTargetLabels,
  targetHref,
  type ReportQueueRow,
} from '@/features/reports/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ReportQueueCard.module.css';

interface ReportQueueCardProps {
  row: ReportQueueRow;
  onChanged: () => void;
}

/**
 * One report in the staff queue, and the two ways out of it.
 *
 * A closed report keeps showing: the moderator's own note and who closed it are the
 * record of what was decided, and hiding the row the moment it closes would take that
 * record off the screen. An open report gets a note box and the two buttons, and a
 * report another moderator closed in the meantime answers 409 — which reloads the queue
 * rather than pretending the second click landed, because the row's status has moved.
 */
export function ReportQueueCard({ row, onChanged }: ReportQueueCardProps) {
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState<'resolve' | 'dismiss' | null>(null);
  const [error, setError] = useState<string | null>(null);

  const href = targetHref(row);
  const isOpen = row.status === 'Open';

  async function close(status: 'Resolved' | 'Dismissed') {
    setBusy(status === 'Resolved' ? 'resolve' : 'dismiss');
    setError(null);

    try {
      await closeReport(row.id, { status, resolutionNote: note.trim() || null });
      onChanged();
    } catch (closeError) {
      setBusy(null);

      if (closeError instanceof ApiError && closeError.code === 'report_closed') {
        setError('Another moderator has already closed this report.');
        onChanged();
      } else {
        setError(getErrorMessage(closeError, 'Could not save the decision. Please try again.'));
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
          {statusLabel(row.status)}
        </span>
        <p className={styles.reason}>{reportReasonLabels[row.reason]}</p>
        <p className={styles.filed}>
          Reported by {row.reporterName} · {formatFiledAt(row.createdAt)}
        </p>
      </div>

      <p className={styles.target}>
        <span className={styles.targetType}>{reportTargetLabels[row.targetType]}</span>
        {href ? (
          <Link to={href} className={styles.targetLink}>
            {row.targetTitle ?? 'This listing is no longer available'}
          </Link>
        ) : (
          <span className={styles.targetLink}>{row.targetTitle ?? 'Listing removed'}</span>
        )}
      </p>

      {row.note ? (
        <p className={styles.note}>&ldquo;{row.note}&rdquo;</p>
      ) : null}

      {!isOpen ? (
        <p className={styles.decision}>
          {statusLabel(row.status)} by {row.resolvedByName ?? 'staff'}
          {row.resolvedAt ? ` · ${formatFiledAt(row.resolvedAt)}` : ''}
          {row.resolutionNote ? ` — “${row.resolutionNote}”` : ''}
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
            <span className={styles.noteLabel}>Resolution note (optional)</span>
            <input
              className={styles.noteInput}
              type="text"
              maxLength={500}
              value={note}
              disabled={busy !== null}
              placeholder="What was decided."
              onChange={(event) => setNote(event.target.value)}
            />
          </label>

          <div className={styles.buttons}>
            <Button
              type="button"
              size="small"
              variant="secondary"
              disabled={busy !== null}
              onClick={() => void close('Resolved')}
            >
              {busy === 'resolve' ? 'Saving…' : 'Resolve'}
            </Button>
            <Button
              type="button"
              size="small"
              variant="ghost"
              disabled={busy !== null}
              onClick={() => void close('Dismissed')}
            >
              {busy === 'dismiss' ? 'Saving…' : 'Dismiss'}
            </Button>
          </div>
        </div>
      ) : null}
    </li>
  );
}
