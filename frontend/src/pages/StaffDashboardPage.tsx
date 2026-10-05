import { Link } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { attentionSections, quickLinks } from '@/features/dashboard/attention';
import { useStaffDashboard } from '@/features/dashboard/useStaffDashboard';
import type { DashboardCounts } from '@/features/dashboard/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './StaffDashboardPage.module.css';

/**
 * The eight numbers, in the order a staff member meets them: people first, then the modules
 * that ship public content, then the two libraries. Accounts are first because they are the
 * only count here that cannot be filtered, published or expired.
 */
const countCards: { key: keyof DashboardCounts; label: string }[] = [
  { key: 'users', label: 'Users' },
  { key: 'businesses', label: 'Businesses' },
  { key: 'jobs', label: 'Jobs' },
  { key: 'deals', label: 'Offers' },
  { key: 'events', label: 'Events' },
  { key: 'studentResources', label: 'Student resources' },
  { key: 'quizQuestions', label: 'Quiz questions' },
  { key: 'calculators', label: 'Calculators' },
];

/**
 * One screen over every module. The counts are the same totals each management page reports
 * and the attention rows are the same rules the public site applies, so nothing here can
 * disagree with the page it links to — which is the whole reason a dashboard is worth having.
 */
export function StaffDashboardPage() {
  const dashboard = useStaffDashboard();
  const sections = dashboard.status === 'success' ? attentionSections(dashboard.data) : [];

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Staff dashboard</h1>
        <p className={styles.subtitle}>
          Every row the site holds, counted from the tables the management pages read. Drafts are
          included, because deciding what to publish next is the work this screen exists for.
        </p>
      </header>

      {dashboard.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading the dashboard" />
      ) : null}

      {dashboard.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the dashboard"
          description={getErrorMessage(
            dashboard.error,
            'The counts are read from the catalogue, so there is nothing to show instead.',
          )}
          action={
            <Button variant="secondary" size="small" onClick={dashboard.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {dashboard.status === 'success' ? (
        <section aria-labelledby="dashboard-counts" className={styles.section}>
          <h2 id="dashboard-counts" className={styles.sectionTitle}>
            The catalogue
          </h2>
          <ul className={styles.countGrid}>
            {countCards.map((card) => (
              <li key={card.key} className={styles.countCard}>
                <span className={styles.countValue}>{dashboard.data.counts[card.key]}</span>
                <span className={styles.countLabel}>{card.label}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {dashboard.status === 'success' ? (
        sections.length > 0 ? (
          <section aria-labelledby="dashboard-attention" className={styles.section}>
            <h2 id="dashboard-attention" className={styles.sectionTitle}>
              Needs attention
            </h2>
            {sections.map((section) => (
              <div key={section.key} className={styles.attentionGroup}>
                <h3 className={styles.attentionTitle}>{section.title}</h3>
                <ul className={styles.attentionList}>
                  {section.rows.map((row) => (
                    <li key={row.key} className={styles.attentionRow}>
                      <Link to={row.to} className={styles.attentionLink}>
                        {row.label}
                      </Link>
                      <span className={styles.attentionCount}>{row.count}</span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </section>
        ) : (
          <section aria-labelledby="dashboard-attention" className={styles.section}>
            <h2 id="dashboard-attention" className={styles.sectionTitle}>
              Needs attention
            </h2>
            <StateMessage
              tone="empty"
              title="Nothing needs attention"
              description="No drafts are waiting and every published row is one a reader can still see."
            />
          </section>
        )
      ) : null}

      <section aria-labelledby="dashboard-links" className={styles.section}>
        <h2 id="dashboard-links" className={styles.sectionTitle}>
          Quick links
        </h2>
        <ul className={styles.quickLinks}>
          {quickLinks.map((link) => (
            <li key={link.key}>
              <Link to={link.to} className={styles.quickLink}>
                <span>{link.label}</span>
                <span aria-hidden="true">→</span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </Container>
  );
}
