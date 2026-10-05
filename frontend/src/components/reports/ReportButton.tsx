import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { useAuth } from '@/features/auth/AuthContext';
import { fileReport } from '@/features/reports/api';
import {
  reportReasonLabels,
  reportReasons,
  type ReportReason,
  type ReportTargetType,
} from '@/features/reports/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ReportButton.module.css';

interface ReportButtonProps {
  targetType: ReportTargetType;
  targetId: string;
}

type Phase = 'idle' | 'form' | 'sending' | 'done';

/**
 * A member reporting the listing they are looking at.
 *
 * The button exists only for a signed-in member, because filing is signed-in only: a
 * report with no account behind it has nobody to follow up with and nothing to stop one
 * visitor filing hundreds. Two answers the API gives get their own sentence rather than
 * an error — "you have already reported this listing" (409) and "this listing is no
 * longer available" (404) — while anything else is shown as-is with a chance to retry.
 *
 * The confirmation stays on screen: the member's part is finished at that point, and
 * replacing the form with a sentence is the clearest way to say so.
 */
export function ReportButton({ targetType, targetId }: ReportButtonProps) {
  const { status } = useAuth();
  const [phase, setPhase] = useState<Phase>('idle');
  const [reason, setReason] = useState<ReportReason>('Spam');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);

  if (status !== 'authenticated') {
    return null;
  }

  if (phase === 'done') {
    return (
      <p className={styles.done} role="status">
        Thanks — your report is with the moderators.
      </p>
    );
  }

  if (phase === 'idle') {
    return (
      <Button variant="ghost" size="small" onClick={() => setPhase('form')}>
        Report this listing
      </Button>
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPhase('sending');
    setError(null);

    try {
      await fileReport({
        targetType,
        targetId,
        reason,
        note: note.trim() ? note.trim() : null,
      });
      setPhase('done');
    } catch (submitError) {
      setPhase('form');

      if (submitError instanceof ApiError && submitError.code === 'already_reported') {
        setError('You have already reported this listing. It is in the queue.');
      } else if (submitError instanceof ApiError && submitError.code === 'target_not_found') {
        setError('This listing is no longer available to report.');
      } else {
        setError(getErrorMessage(submitError, 'Could not send the report. Please try again.'));
      }
    }
  }

  const isSending = phase === 'sending';

  return (
    <form className={styles.form} onSubmit={(event) => void handleSubmit(event)}>
      <p className={styles.heading}>Report this listing</p>

      <label className={styles.field}>
        <span className={styles.label}>Reason</span>
        <select
          className={styles.select}
          value={reason}
          disabled={isSending}
          onChange={(event) => setReason(event.target.value as ReportReason)}
        >
          {reportReasons.map((value) => (
            <option key={value} value={value}>
              {reportReasonLabels[value]}
            </option>
          ))}
        </select>
      </label>

      <label className={styles.field}>
        <span className={styles.label}>Note (optional)</span>
        <textarea
          className={styles.textarea}
          value={note}
          disabled={isSending}
          maxLength={500}
          rows={3}
          placeholder="Anything the moderators should know."
          onChange={(event) => setNote(event.target.value)}
        />
      </label>

      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : null}

      <div className={styles.actions}>
        <Button type="submit" size="small" disabled={isSending}>
          {isSending ? 'Sending…' : 'Send report'}
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="small"
          disabled={isSending}
          onClick={() => {
            setPhase('idle');
            setError(null);
          }}
        >
          Cancel
        </Button>
      </div>
    </form>
  );
}
