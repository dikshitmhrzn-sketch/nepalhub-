import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { useAuth } from '@/features/auth/AuthContext';
import { fileClaim } from '@/features/ownership/api';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ClaimPanel.module.css';

interface ClaimPanelProps {
  slug: string;
}

type Phase = 'idle' | 'form' | 'sending' | 'done';

/**
 * A member asserting that the listing in front of them is theirs.
 *
 * The claim never grants access — a moderator decides, and approval is what moves ownership —
 * so this panel's whole job is to file the assertion and say where it went. Anonymous readers
 * get the sentence explaining that an account is needed, because a claim with nobody behind it
 * has no one to review and nothing to stop a flood of them. The four conflicts the API gives
 * each get their own sentence: already managed, already owned, already waiting, gone.
 */
export function ClaimPanel({ slug }: ClaimPanelProps) {
  const { status } = useAuth();
  const [phase, setPhase] = useState<Phase>('idle');
  const [evidence, setEvidence] = useState('');
  const [error, setError] = useState<string | null>(null);

  if (status === 'anonymous') {
    return (
      <p className={styles.signIn}>
        Own this business?{' '}
        <Link to="/login" className={styles.signInLink}>
          Sign in
        </Link>{' '}
        to claim the listing and manage its details.
      </p>
    );
  }

  if (status === 'loading') {
    return null;
  }

  if (phase === 'done') {
    return (
      <p className={styles.done} role="status">
        Your claim is with our staff. Ownership changes only when they approve it — until then
        nothing about the listing has changed. You can follow it from{' '}
        <Link to="/my-business" className={styles.signInLink}>
          My businesses
        </Link>
        .
      </p>
    );
  }

  if (phase === 'idle') {
    return (
      <div className={styles.panel}>
        <p className={styles.heading}>Is this your business?</p>
        <p className={styles.body}>
          Claim the listing to correct its details and manage its plan. Staff review every claim
          before ownership changes.
        </p>
        <Button variant="secondary" size="small" onClick={() => setPhase('form')}>
          Claim this listing
        </Button>
      </div>
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPhase('sending');
    setError(null);

    try {
      await fileClaim(slug, evidence.trim() ? evidence.trim() : null);
      setPhase('done');
    } catch (submitError) {
      setPhase('form');

      if (submitError instanceof ApiError) {
        switch (submitError.code) {
          case 'already_owner':
            setError('You already manage this listing.');
            break;
          case 'already_owned':
            setError(
              'This listing already has an owner, so it cannot be claimed. If the details are wrong, report the listing instead.',
            );
            break;
          case 'already_claimed':
            setError('You already have a claim waiting on this listing. Staff are reviewing it.');
            break;
          default:
            setError(getErrorMessage(submitError, 'Could not send the claim. Please try again.'));
        }
      } else {
        setError(getErrorMessage(submitError, 'Could not send the claim. Please try again.'));
      }
    }
  }

  const isSending = phase === 'sending';

  return (
    <form className={styles.form} onSubmit={(event) => void handleSubmit(event)}>
      <p className={styles.heading}>Claim this listing</p>

      <label className={styles.field}>
        <span className={styles.label}>How are you connected to this business? (optional)</span>
        <textarea
          className={styles.textarea}
          value={evidence}
          disabled={isSending}
          maxLength={500}
          rows={3}
          placeholder="For example: I run it, or I am the manager listed on the sign."
          onChange={(event) => setEvidence(event.target.value)}
        />
      </label>

      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : null}

      <div className={styles.actions}>
        <Button type="submit" size="small" disabled={isSending}>
          {isSending ? 'Sending…' : 'Send claim'}
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
