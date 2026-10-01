import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import { useApiHealth } from '@/features/health/useApiHealth';

import styles from './HomePage.module.css';

const databaseBadges: Record<string, { label: string; tone: string }> = {
  connected: { label: 'connected', tone: 'badgeSuccess' },
  'not-configured': { label: 'not configured yet', tone: 'badgeMuted' },
  unreachable: { label: 'unreachable', tone: 'badgeWarning' },
  timeout: { label: 'timed out', tone: 'badgeWarning' },
};

function StatusCard() {
  const health = useApiHealth();

  if (health.status === 'loading') {
    return <StateMessage tone="loading" title="Checking the Nepal Hub API…" />;
  }

  if (health.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not reach the API"
        description={`${health.error.message} (code: ${health.error.code})`}
      />
    );
  }

  const database = databaseBadges[health.data.database] ?? {
    label: health.data.database,
    tone: 'badgeMuted',
  };

  return (
    <div className={styles.card}>
      <div className={styles.cardTitle}>API status</div>

      <div className={styles.statusList}>
        <div className={styles.statusRow}>
          <span className={styles.statusKey}>Service</span>
          <span className={`${styles.badge} ${styles.badgeSuccess}`}>
            <span className={styles.dot} aria-hidden="true" />
            {health.data.status}
          </span>
        </div>

        <div className={styles.statusRow}>
          <span className={styles.statusKey}>Database</span>
          <span className={`${styles.badge} ${styles[database.tone]}`}>{database.label}</span>
        </div>

        <div className={styles.statusRow}>
          <span className={styles.statusKey}>Checked at</span>
          <span className={styles.statusValue}>
            {new Date(health.data.timestamp).toLocaleTimeString()}
          </span>
        </div>
      </div>
    </div>
  );
}

const foundations = [
  {
    title: 'Separate apps',
    text: 'React talks to the API over HTTP only, so a Flutter mobile app can reuse the same endpoints later.',
  },
  {
    title: 'MySQL through EF Core',
    text: 'MySQL 8.4 runs on its own port and the migrations create the users, roles, reference data and calculators tables.',
  },
  {
    title: 'Tools and calculators',
    text: 'The first public module is live. Each calculator describes its own form as data, and every figure is computed on the server by a registered handler.',
  },
  {
    title: 'Real accounts',
    text: 'Registration, sign-in, lockout and role-based access are handled by ASP.NET Core Identity with a secure cookie.',
  },
  {
    title: 'Secrets stay on the server',
    text: 'The browser only knows a base URL. Database credentials live in ASP.NET Core user-secrets.',
  },
  {
    title: 'Mobile-first styling',
    text: 'Design tokens and component styles are written for 320px screens and scale upward.',
  },
];

export function HomePage() {
  const { status, user } = useAuth();

  return (
    <>
      <section className={styles.hero}>
        <Container className={styles.heroGrid}>
          <div>
            <span className={styles.eyebrow}>Accounts, data, tools, quiz and the Student Hub</span>
            <h1 className={styles.title}>One useful website for everyday Nepal.</h1>
            <p className={styles.lead}>
              Nepal Hub will bring tools, a daily quiz, study help, jobs, local businesses, deals and
              events into one place. Accounts, the real database, the first tools and the Student Hub
              are now in place — the remaining modules are still to come.
            </p>

            <div className={styles.heroActions}>
              <a className={styles.primaryLink} href="/tools">
                Browse the tools
              </a>
              <a className={styles.secondaryLink} href="/students">
                Student Hub
              </a>
              {status === 'authenticated' && user ? (
                <>
                  <p className={styles.signedInAs}>Signed in as {user.fullName}.</p>
                  <a className={styles.secondaryLink} href="/account">
                    Go to your account
                  </a>
                </>
              ) : (
                <>
                  <a className={styles.secondaryLink} href="/register">
                    Create an account
                  </a>
                  <a className={styles.secondaryLink} href="/login">
                    Sign in
                  </a>
                </>
              )}
            </div>
          </div>


          <div>
            <StatusCard />
          </div>
        </Container>
      </section>

      <section className={styles.section}>
        <Container>
          <h2 className={styles.sectionTitle}>What is already in place</h2>
          <p className={styles.sectionText}>
            The decisions that every future module depends on.
          </p>

          <div className={styles.grid}>
            {foundations.map((item) => (
              <article key={item.title} className={styles.card}>
                <h3 className={styles.cardTitle}>{item.title}</h3>
                <p className={styles.cardText}>{item.text}</p>
              </article>
            ))}
          </div>
        </Container>
      </section>
    </>
  );
}
