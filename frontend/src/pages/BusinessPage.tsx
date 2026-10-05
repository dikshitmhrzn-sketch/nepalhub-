import { Link, useParams } from 'react-router';

import { ReportButton } from '@/components/reports/ReportButton';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import {
  categoryLabel,
  describeHours,
  formatLocation,
  formatPhone,
  openNowLabel,
  openNowTone,
} from '@/features/businesses/format';
import { useBusiness } from '@/features/businesses/useBusinesses';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './BusinessPage.module.css';

export function BusinessPage() {
  const { slug = '' } = useParams();
  const business = useBusiness(slug);

  if (business.status === 'loading') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Loading…" />
      </Container>
    );
  }

  if (business.status === 'error') {
    const isMissing = business.error.status === 404;

    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title={isMissing ? 'That business is not listed' : 'Could not load the listing'}
          description={
            isMissing
              ? 'It may have been taken off the directory, or the link may be out of date. The directory lists everything that is still published.'
              : getErrorMessage(business.error, 'Please try again in a moment.')
          }
          action={
            isMissing ? (
              <Link to="/businesses" className={styles.backLink}>
                Back to the directory
              </Link>
            ) : (
              <Button variant="secondary" size="small" onClick={business.reload}>
                Try again
              </Button>
            )
          }
        />
      </Container>
    );
  }

  const detail = business.data;
  const tone = openNowTone(detail.isOpenNow);
  const hasHours = detail.openingHours.length > 0;

  return (
    <Container as="section" className={styles.page}>
      <Link to="/businesses" className={styles.backLink}>
        ← Back to the directory
      </Link>

      <article className={styles.article}>
        <header className={styles.header}>
          <div className={styles.badges}>
            <p className={styles.category}>{categoryLabel(detail.category)}</p>
            {detail.isVerified ? <span className={styles.verified}>Verified</span> : null}
            <span className={`${styles.openBadge} ${styles[tone]}`}>
              {openNowLabel(detail.isOpenNow)}
            </span>
          </div>

          <h1 className={styles.title}>{detail.name}</h1>
          <p className={styles.organization}>
            {formatLocation(detail.provinceName, detail.districtName)}
          </p>
        </header>

        <dl className={styles.facts}>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Category</dt>
            <dd className={styles.factValue}>{categoryLabel(detail.category)}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>District</dt>
            <dd className={styles.factValue}>{detail.districtName}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Province</dt>
            <dd className={styles.factValue}>{detail.provinceName}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Checked</dt>
            <dd className={styles.factValue}>
              {detail.isVerified ? 'By a member of our staff' : 'Not yet checked'}
            </dd>
          </div>
        </dl>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>About this business</h2>
          {/*
            The description is plain text from the API, rendered as text. Splitting on blank lines
            keeps the paragraphs a staff member typed without ever handing the content to
            dangerouslySetInnerHTML, so angle brackets in a listing stay angle brackets.
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

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Opening hours</h2>

          {/*
            The API returns the whole week, Sunday first, whenever a listing published any hours at
            all, and an empty list when it published none. The two are rendered differently on
            purpose: an empty list means nobody stated a schedule, which is not the same statement
            as being shut all week.
          */}
          {hasHours ? (
            <>
              <dl className={styles.hours}>
                {detail.openingHours.map((day) => {
                  const isClosed = day.isClosed;
                  const overnight =
                    !isClosed &&
                    day.opensAt !== null &&
                    day.closesAt !== null &&
                    day.closesAt < day.opensAt;

                  return (
                    <div className={styles.hoursRow} key={day.dayOfWeek}>
                      <dt className={styles.hoursDay}>{day.dayOfWeek}</dt>
                      <dd
                        className={`${styles.hoursValue} ${
                          isClosed
                            ? styles.hoursValueClosed
                            : overnight
                              ? styles.hoursValueOvernight
                              : ''
                        }`}
                      >
                        {describeHours(day.opensAt, day.closesAt, day.isClosed)}
                      </dd>
                    </div>
                  );
                })}
              </dl>
              <p className={styles.hoursNote}>
                Times are Nepal local time. &ldquo;Next day&rdquo; means the business closes after
                midnight.
              </p>
            </>
          ) : (
            <p className={styles.hoursNote}>
              This business has not published its opening hours, so Nepal Hub cannot say whether it
              is open. Please call ahead.
            </p>
          )}
        </section>

        <div className={styles.contact}>
          {detail.address ? (
            <p className={styles.contactRow}>
              <span className={styles.contactLabel}>Address</span>
              <span className={styles.contactValue}>{detail.address}</span>
            </p>
          ) : null}

          {detail.phone ? (
            <p className={styles.contactRow}>
              <span className={styles.contactLabel}>Phone</span>
              <span className={styles.contactValue}>
                <a href={`tel:${detail.phone}`}>{formatPhone(detail.phone)}</a>
              </span>
            </p>
          ) : null}

          {detail.email ? (
            <p className={styles.contactRow}>
              <span className={styles.contactLabel}>Email</span>
              <span className={styles.contactValue}>
                <a href={`mailto:${detail.email}`}>{detail.email}</a>
              </span>
            </p>
          ) : null}

          {detail.website ? (
            <p className={styles.contactRow}>
              <span className={styles.contactLabel}>Website</span>
              <span className={styles.contactValue}>
                {/*
                  The API accepts only an absolute https address, so this is a link a staff member
                  chose rather than anything a reader typed. noreferrer and noopener keep the new
                  tab from reaching this page or its opener.
                */}
                <a href={detail.website} target="_blank" rel="noreferrer noopener">
                  {detail.website} ↗
                </a>
              </span>
            </p>
          ) : null}

          <p className={styles.contactNote}>
            {detail.isVerified
              ? 'A member of our staff has checked this listing. Nepal Hub stores these details only and never contacts the business on your behalf.'
              : 'This listing has not been checked by our staff yet, so treat the details as a starting point rather than a promise.'}
          </p>
        </div>
        <ReportButton targetType="Business" targetId={detail.id} />
      </article>
    </Container>
  );
}
