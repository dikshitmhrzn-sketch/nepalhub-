import { Link, useParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  categoryLabel,
  formatAward,
  formatDeadline,
  formatUpdatedAt,
  levelLabel,
  typeLabel,
} from '@/features/students/format';
import { useResource } from '@/features/students/useResources';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './StudentResourcePage.module.css';

export function StudentResourcePage() {
  const { slug = '' } = useParams();
  const resource = useResource(slug);

  if (resource.status === 'loading') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Loading…" />
      </Container>
    );
  }

  if (resource.status === 'error') {
    const isMissing = resource.error.status === 404;

    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title={isMissing ? 'That resource is not here' : 'Could not load the resource'}
          description={
            isMissing
              ? 'It may have been a draft, or it may have passed its closing date. Either way it is no longer listed.'
              : getErrorMessage(resource.error, 'Please try again in a moment.')
          }
          action={
            isMissing ? (
              <Link to="/students" className={styles.backLink}>
                Back to the Student Hub
              </Link>
            ) : (
              <Button variant="secondary" size="small" onClick={resource.reload}>
                Try again
              </Button>
            )
          }
        />
      </Container>
    );
  }

  const detail = resource.data;
  const deadline = formatDeadline(detail.expiresOn);
  const award = formatAward(detail.awardAmountNpr);

  return (
    <Container as="section" className={styles.page}>
      <Link to="/students" className={styles.backLink}>
        ← Back to the Student Hub
      </Link>

      <article className={styles.article}>
        <header className={styles.header}>
          <p className={styles.category}>
            {typeLabel(detail.type)} · {categoryLabel(detail.category)}
          </p>

          <h1 className={styles.title}>{detail.title}</h1>

          {detail.summary ? <p className={styles.summary}>{detail.summary}</p> : null}
        </header>

        <dl className={styles.facts}>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Level</dt>
            <dd className={styles.factValue}>{levelLabel(detail.level)}</dd>
          </div>

          {detail.academicYear !== null ? (
            <div className={styles.fact}>
              <dt className={styles.factLabel}>Year</dt>
              <dd className={styles.factValue}>{detail.academicYear} BS</dd>
            </div>
          ) : null}

          {detail.provider ? (
            <div className={styles.fact}>
              <dt className={styles.factLabel}>Offered by</dt>
              <dd className={styles.factValue}>{detail.provider}</dd>
            </div>
          ) : null}

          {award ? (
            <div className={styles.fact}>
              <dt className={styles.factLabel}>Award</dt>
              <dd className={styles.factValue}>{award}</dd>
            </div>
          ) : null}

          {deadline ? (
            <div className={styles.fact}>
              <dt className={styles.factLabel}>Closes</dt>
              <dd className={styles.factValue}>{deadline}</dd>
            </div>
          ) : null}

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Updated</dt>
            <dd className={styles.factValue}>{formatUpdatedAt(detail.updatedAt)}</dd>
          </div>
        </dl>

        {detail.eligibility ? (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Who can apply</h2>
            <p className={styles.paragraph}>{detail.eligibility}</p>
          </section>
        ) : null}

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Details</h2>
          {/*
            The body is plain text from the API, rendered as text. Splitting on blank
            lines keeps the paragraphs a member typed without ever handing the content
            to dangerouslySetInnerHTML, so a link or a script in a note stays a link or
            a bit of text rather than becoming markup.
          */}
          {detail.body
            .split(/\n\s*\n/)
            .map((paragraph) => paragraph.trim())
            .filter((paragraph) => paragraph.length > 0)
            .map((paragraph) => (
              <p className={styles.paragraph} key={paragraph.slice(0, 40)}>
                {paragraph}
              </p>
            ))}
        </section>

        {detail.externalUrl ? (
          <p className={styles.external}>
            <a
              className={styles.externalLink}
              href={detail.externalUrl}
              target="_blank"
              rel="noreferrer noopener"
            >
              {detail.type === 'Scholarship' ? 'Read the full announcement' : 'Open the paper'} ↗
            </a>
            <span className={styles.externalNote}>
              Opens on the publisher&rsquo;s own site. Nepal Hub stores the address only and never
              fetches it.
            </span>
          </p>
        ) : null}
      </article>
    </Container>
  );
}
