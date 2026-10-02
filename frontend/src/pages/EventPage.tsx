import { Link, useParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  categoryLabel,
  describeWhen,
  formatEventDate,
  formatVenue,
  notFoundMessage,
} from '@/features/events/format';
import { useEvent } from '@/features/events/useEvents';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './EventPage.module.css';

export function EventPage() {
  const { slug = '' } = useParams();
  const event = useEvent(slug);

  if (event.status === 'loading') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Loading…" />
      </Container>
    );
  }

  if (event.status === 'error') {
    const isMissing = event.error.status === 404;

    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title={isMissing ? 'That event is not listed' : 'Could not load the event'}
          description={
            isMissing
              ? notFoundMessage('The event')
              : getErrorMessage(event.error, 'Please try again in a moment.')
          }
          action={
            isMissing ? (
              <Link to="/events" className={styles.backLink}>
                Back to the events
              </Link>
            ) : (
              <Button variant="secondary" size="small" onClick={event.reload}>
                Try again
              </Button>
            )
          }
        />
      </Container>
    );
  }

  const detail = event.data;
  const venue = formatVenue(detail.venue, detail.districtName);

  return (
    <Container as="section" className={styles.page}>
      <Link to="/events" className={styles.backLink}>
        ← Back to the events
      </Link>

      <article className={styles.article}>
        <header className={styles.header}>
          <div className={styles.badges}>
            <p className={styles.category}>{categoryLabel(detail.category)}</p>
            {detail.isOngoing ? <span className={styles.ongoing}>Happening now</span> : null}
          </div>

          <h1 className={styles.title}>{detail.title}</h1>
          {detail.summary ? <p className={styles.summary}>{detail.summary}</p> : null}
        </header>

        <dl className={styles.facts}>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Starts</dt>
            <dd className={styles.factValue}>{formatEventDate(detail.startsAt)}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Ends</dt>
            <dd className={styles.factValue}>{formatEventDate(detail.endsAt)}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Where</dt>
            <dd className={styles.factValue}>{venue ?? 'Not stated'}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Organiser</dt>
            <dd className={styles.factValue}>{detail.organiser ?? 'Not stated'}</dd>
          </div>
        </dl>

        <p className={styles.when}>{describeWhen(detail.startsAt, detail.endsAt, detail.isOngoing)}</p>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>About this event</h2>
          {/*
            The description is plain text from the API, rendered as text. Splitting on blank lines
            keeps the paragraphs a staff member typed without ever handing the content to
            dangerouslySetInnerHTML.
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

        {detail.businessName ? (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Who is running this</h2>
            <p className={styles.paragraph}>
              {/*
                As with offers, the organiser's business name survives the listing being
                retracted, so the event stays readable. Only a published business sends a slug, and
                only that case is worth linking.
              */}
              {detail.businessSlug ? (
                <Link to={`/businesses/${encodeURIComponent(detail.businessSlug)}`}>
                  {detail.businessName}
                </Link>
              ) : (
                detail.businessName
              )}
            </p>
          </section>
        ) : null}

        {detail.registrationUrl ? (
          <p className={styles.cta}>
            <a
              className={styles.ctaLink}
              href={detail.registrationUrl}
              target="_blank"
              rel="noreferrer noopener"
            >
              Register or find out more ↗
            </a>
          </p>
        ) : null}

        <p className={styles.note}>
          Times are Nepal Standard Time. Nepal Hub stores these details only and never fetches the
          organiser&rsquo;s page, so please check the times with them before travelling.
        </p>
      </article>
    </Container>
  );
}