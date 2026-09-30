import type { ReactNode } from 'react';

import styles from './StateMessage.module.css';

type StateTone = 'loading' | 'empty' | 'error' | 'info';

interface StateMessageProps {
  tone: StateTone;
  title: string;
  description?: ReactNode;
  action?: ReactNode;
  'aria-live'?: 'polite' | 'assertive';
}

/**
 * Renders a single status view: loading, empty, error or informational.
 * Data-fetching components use this instead of inventing their own markup.
 */
export function StateMessage({
  tone,
  title,
  description,
  action,
  'aria-live': ariaLive = 'polite',
}: StateMessageProps) {
  return (
    <div
      className={styles.state}
      role={tone === 'error' ? 'alert' : 'status'}
      aria-live={tone === 'error' ? 'assertive' : ariaLive}
      aria-busy={tone === 'loading' || undefined}
    >
      {tone === 'loading' ? <span className={styles.spinner} aria-hidden="true" /> : null}
      <p className={styles.title}>{title}</p>
      {description ? <p className={styles.description}>{description}</p> : null}
      {action}
    </div>
  );
}
