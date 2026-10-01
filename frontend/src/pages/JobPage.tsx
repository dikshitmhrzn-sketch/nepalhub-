import { Link, useParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  categoryLabel,
  describeJob,
  formatDeadline,
  formatLocation,
  formatPostedAt,
  statusLabel,
} from '@/features/jobs/format';
import { useJob } from '@/features/jobs/useJobs';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './JobPage.module.css';

export function JobPage() {
  const { slug = '' } = useParams();
  const job = useJob(slug);

  if (job.status === 'loading') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Loading…" />
      </Container>
    );
  }

  if (job.status === 'error') {
    const isMissing = job.error.status === 404;

    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title={isMissing ? 'That job is not here' : 'Could not load the job'}
          description={
            isMissing
              ? 'It may have closed, or the link may be out of date. The job board lists everything that is still open.'
              : getErrorMessage(job.error, 'Please try again in a moment.')
          }
          action={
            isMissing ? (
              <Link to="/jobs" className={styles.backLink}>
                Back to the job board
              </Link>
            ) : (
              <Button variant="secondary" size="small" onClick={job.reload}>
                Try again
              </Button>
            )
          }
        />
      </Container>
    );
  }

  const detail = job.data;
  const location = formatLocation(detail.provinceName, detail.districtName);
  const isClosingSoon = detail.status === 'ClosingSoon';

  return (
    <Container as="section" className={styles.page}>
      <Link to="/jobs" className={styles.backLink}>
        ← Back to the job board
      </Link>

      <article className={styles.article}>
        <header className={styles.header}>
          <div className={styles.badges}>
            <p className={styles.category}>{categoryLabel(detail.category)}</p>
            {isClosingSoon ? (
              <span className={styles.closingSoon}>{statusLabel(detail.status)}</span>
            ) : null}
          </div>

          <h1 className={styles.title}>{detail.title}</h1>
          <p className={styles.organization}>{detail.organization}</p>
        </header>

        <dl className={styles.facts}>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Employment</dt>
            <dd className={styles.factValue}>
              {describeJob(detail.employmentType, detail.workMode)}
            </dd>
          </div>

          {location ? (
            <div className={styles.fact}>
              <dt className={styles.factLabel}>Location</dt>
              <dd className={styles.factValue}>{location}</dd>
            </div>
          ) : null}

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Closes</dt>
            <dd className={isClosingSoon ? styles.factUrgent : styles.factValue}>
              {formatDeadline(detail.applicationDeadline)}
            </dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Last updated</dt>
            <dd className={styles.factValue}>{formatPostedAt(detail.updatedAt)}</dd>
          </div>
        </dl>

        {isClosingSoon ? (
          <p className={styles.closingNote}>
            This posting closes on {formatDeadline(detail.applicationDeadline)}. If you are going to
            apply, do it soon.
          </p>
        ) : null}

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>About this role</h2>
          {/*
            The description is plain text from the API, rendered as text. Splitting on blank
            lines keeps the paragraphs a staff member typed without ever handing the content to
            dangerouslySetInnerHTML, so a link or a script in a posting stays a link or a bit of
            text rather than becoming markup.
          */}
          {detail.description
            .split(/\n\s*\n/)
            .map((paragraph) => paragraph.trim())
            .filter((paragraph) => paragraph.length > 0)
            .map((paragraph) => (
              <p className={styles.paragraph} key={paragraph.slice(0, 40)}>
                {paragraph}
              </p>
            ))}
        </section>

        <p className={styles.external}>
          <a
            className={styles.externalLink}
            href={detail.applicationUrl}
            target="_blank"
            rel="noreferrer noopener"
          >
            Apply on the employer&rsquo;s site ↗
          </a>
          <span className={styles.externalNote}>
            Nepal Hub does not take applications. The link opens on the employer&rsquo;s own site, and
            Nepal Hub stores the address only and never fetches it.
          </span>
        </p>
      </article>
    </Container>
  );
}