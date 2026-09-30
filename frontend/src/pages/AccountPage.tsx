import { useState } from 'react';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './AccountPage.module.css';

export function AccountPage() {
  const { user, isAdmin, signOut } = useAuth();

  const [isSigningOut, setIsSigningOut] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!user) {
    return <StateMessage tone="loading" title="Loading your account" />;
  }

  async function handleSignOut() {
    setIsSigningOut(true);
    setError(null);

    try {
      await signOut();
    } catch {
      setError(getErrorMessage(null, 'Could not sign you out. Please try again.'));
      setIsSigningOut(false);
    }
  }

  return (
    <Container as="section" className={styles.page}>
      <div className={styles.panel}>
        <header className={styles.header}>
          <div>
            <h1 className={styles.title}>{user.fullName}</h1>
            <p className={styles.subtitle}>Your Nepal Hub account</p>
          </div>

          <Button
            variant="secondary"
            onClick={() => void handleSignOut()}
            disabled={isSigningOut}
          >
            {isSigningOut ? 'Signing out…' : 'Sign out'}
          </Button>
        </header>

        {error ? (
          <p className={styles.notice} role="alert">
            {error}
          </p>
        ) : null}

        <dl className={styles.details}>
          <div className={styles.row}>
            <dt className={styles.key}>Email</dt>
            <dd className={styles.value}>{user.email}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>Email confirmed</dt>
            <dd className={styles.value}>{user.isEmailConfirmed ? 'Yes' : 'Not yet'}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>Level</dt>
            <dd className={styles.value}>{user.level}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>XP</dt>
            <dd className={styles.value}>{user.xp}</dd>
          </div>
          <div className={styles.row}>
            <dt className={styles.key}>Roles</dt>
            <dd className={styles.value}>
              <ul className={styles.roles}>
                {user.roles.map((role) => (
                  <li className={styles.role} key={role}>
                    {role}
                  </li>
                ))}
              </ul>
            </dd>
          </div>
        </dl>

        <p className={styles.notice}>
          Accounts start confirmed because email sending is not connected yet. Saved tools, the quiz
          streak and the rest of your profile arrive with the next phases.
          {isAdmin ? ' You have administrator access.' : ''}
        </p>
      </div>
    </Container>
  );
}
