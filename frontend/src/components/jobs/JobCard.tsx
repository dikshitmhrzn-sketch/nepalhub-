import { Link } from 'react-router';

import {
  categoryLabel,
  describeJob,
  formatDeadline,
  formatLocation,
  statusLabel,
} from '@/features/jobs/format';
import type { JobSummary } from '@/features/jobs/types';

import styles from './JobCard.module.css';

interface JobCardProps {
  job: JobSummary;
}

/**
 * One row in the public job board. The title is the link.
 *
 * The card carries no description on purpose: the list endpoint projects a summary without it,
 * and a page of cards is scannable precisely because each one is short.
 */
export function JobCard({ job }: JobCardProps) {
  const location = formatLocation(job.provinceName, job.districtName);
  const isClosingSoon = job.status === 'ClosingSoon';

  return (
    <article className={styles.card}>
      <div className={styles.badges}>
        <p className={styles.category}>{categoryLabel(job.category)}</p>
        {isClosingSoon ? (
          <span className={styles.closingSoon}>{statusLabel(job.status)}</span>
        ) : null}
      </div>

      <h3 className={styles.name}>
        <Link to={`/jobs/${job.slug}`} className={styles.link}>
          {job.title}
        </Link>
      </h3>

      <p className={styles.organization}>{job.organization}</p>

      <div className={styles.footer}>
        <span className={styles.meta}>{describeJob(job.employmentType, job.workMode)}</span>

        {location ? <span className={styles.meta}>{location}</span> : null}

        <span className={isClosingSoon ? styles.deadlineUrgent : styles.deadline}>
          <span className={styles.deadlineLabel}>Closes</span> {formatDeadline(job.applicationDeadline)}
        </span>
      </div>
    </article>
  );
}