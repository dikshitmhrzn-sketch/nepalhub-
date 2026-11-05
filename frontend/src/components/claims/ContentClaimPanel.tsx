import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { useAuth } from '@/features/auth/AuthContext';
import { fileContentClaim } from '@/features/claims/api';
import type { ContentClaimKind } from '@/features/claims/types';
import { ApiError } from '@/lib/apiClient';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ClaimPanel.module.css';

interface ContentClaimPanelProps {
  kind: ContentClaimKind;
  slug: string;
}

type Phase = 'idle' | 'form' | 'sending' | 'done';

/**
 * A member asserting that the job, offer or event in front of them is theirs.
 *
 * Unlike the business claim, filing is instant: the post leaves every public page in the
 * same request and the poster decides next. Anonymous readers get the sentence explaining
 * that an account is needed, because a claim with nobody behind it has no one to hand it to.
 */
export function ContentClaimPanel({ kind, slug }: ContentClaimPanelProps) {
  const { status } = useAuth();
  const [phase, setPhase] = useState<Phase>('idle');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);

  if (status === 'anonymous') {
    return (
      <p className={styles.signIn}>
        Is this yours?{' '}
        <Link to="/login" className={styles.signInLink}>
          Sign in
        </Link>{' '}
        to claim it. The post leaves the public pages right away and the person who posted it
        decides what happens next.
      </p>
    );
  }

  if (status === 'loading') {
    return null;
  }

  if (phase === 'done') {
    return (
      <p className={styles.done} role="status">
        Claim filed. This post is no longer public. You can follow it from{' '}
        <Link to="/account" className={styles.signInLink}>
          My claims
        </Link>{' '}
        on your account page.
      </p>
    );
  }

  if (phase === 'idle') {
    return (
      <div className={styles.panel}>
        <p className={styles.heading}>Is this yours?</p>
        <p className={styles.body}>
          Claim it and it disappears from the public pages straight away. The person who posted
          it sees your name and can accept the claim or put the post back.
        </p>
        <Button variant="secondary" size="small" onClick={() => setPhase('form')}>
          Claim this post
        </Button>
      </div>
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPhase('sending');
    setError(null);

    try {
      await fileContentClaim(kind, slug, note.trim() ? note.trim() : null);
      setPhase('done');
    } catch (submitError) {
      setPhase('form');

      if (submitError instanceof ApiError) {
        switch (submitError.code) {
          case 'already_taken':
            setError('Someone already claimed this post.');
            break;
          case 'already_claimed':
            setError('You already have an active claim on this post.');
            break;
          case 'no_poster':
            setError('This post has no one to hand the claim to, so it cannot be claimed.');
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
      <p className={styles.heading}>Claim this post</p>

      <label className={styles.field}>
        <span className={styles.label}>Why is this yours? (optional)</span>
        <textarea
          className={styles.textarea}
          value={note}
          disabled={isSending}
          maxLength={500}
          rows={3}
          placeholder="For example: I wrote this, or it is my organisation's posting."
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
