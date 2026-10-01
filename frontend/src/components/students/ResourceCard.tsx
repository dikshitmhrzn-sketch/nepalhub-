import { Link } from 'react-router';

import { describeResource, formatDeadline } from '@/features/students/format';
import type { ResourceSummary } from '@/features/students/types';

import styles from './ResourceCard.module.css';

interface ResourceCardProps {
  resource: ResourceSummary;
}

/** One row in the public catalogue. The title is the link. */
export function ResourceCard({ resource }: ResourceCardProps) {
  const deadline = formatDeadline(resource.expiresOn);

  return (
    <article className={styles.card}>
      <p className={styles.category}>{describeResource(resource.type, resource.category)}</p>

      <h3 className={styles.name}>
        <Link to={`/students/resources/${resource.slug}`} className={styles.link}>
          {resource.title}
        </Link>
      </h3>

      {resource.summary ? <p className={styles.summary}>{resource.summary}</p> : null}

      <div className={styles.footer}>
        {deadline ? (
          <span className={styles.deadline}>
            <span className={styles.deadlineLabel}>Closes</span> {deadline}
          </span>
        ) : (
          <span className={styles.openEnded}>No closing date</span>
        )}

        {resource.hasExternalUrl ? (
          <span className={styles.external}>
            {resource.type === 'Scholarship' ? 'Details elsewhere' : 'PDF link'}
          </span>
        ) : null}
      </div>
    </article>
  );
}
