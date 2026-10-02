import { Link, useParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { describeRun, formatLocation, formatNepalDate, remainingLabel } from '@/features/deals/format';
import { useDeal } from '@/features/deals/useDeals';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './DealPage.module.css';

export function DealPage() {
  const { slug = '' } = useParams();
  const deal = useDeal(slug);

  if (deal.status === 'loading') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Loading…" />
      </Container>
    );
  }

  if (deal.status === 'error') {
    const isMissing = deal.error.status === 404;

    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title={isMissing ? 'That offer is not available' : 'Could not load the offer'}
          description={
            isMissing
              ? 'It may have ended, or the link may be out of date. The offers page lists everything that is still running.'
              : getErrorMessage(deal.error, 'Please try again in a moment.')
          }
          action={
            isMissing ? (
              <Link to="/deals" className={styles.backLink}>
                Back to the offers
              </Link>
            ) : (
              <Button variant="secondary" size="small" onClick={deal.reload}>
                Try again
              </Button>
            )
          }
        />
      </Container>
    );
  }

  const detail = deal.data;
  const location = formatLocation(detail.districtName, detail.provinceName);

  return (
    <Container as="section" className={styles.page}>
      <Link to="/deals" className={styles.backLink}>
        ← Back to the offers
      </Link>

      <article className={styles.article}>
        <header className={styles.header}>
          <div className={styles.badges}>
            {detail.discountText ? (
              <p className={styles.discount}>{detail.discountText}</p>
            ) : null}
            <span className={styles.running} data-running={detail.isRunning}>
              {detail.isRunning ? remainingLabel(detail.daysRemaining) : 'Not started yet'}
            </span>
          </div>

          <h1 className={styles.title}>{detail.title}</h1>
          {detail.summary ? <p className={styles.summary}>{detail.summary}</p> : null}
        </header>

        <dl className={styles.facts}>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Runs</dt>
            <dd className={styles.factValue}>
              {describeRun(detail.startsOn, detail.endsOn, detail.daysRemaining, detail.isRunning)}
            </dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Starts</dt>
            <dd className={styles.factValue}>{formatNepalDate(detail.startsOn)}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Ends</dt>
            <dd className={styles.factValue}>{formatNepalDate(detail.endsOn)}</dd>
          </div>

          <div className={styles.fact}>
            <dt className={styles.factLabel}>Where</dt>
            <dd className={styles.factValue}>{location ?? 'Nationwide'}</dd>
          </div>
        </dl>

        {detail.businessName ? (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Who is offering this</h2>
            <p className={styles.paragraph}>
              {/*
                The API sends the business name on every offer, whether or not the listing is
                published, so the offer stays readable after its business is retracted. Only a
                published business comes with a slug, and only that case gets a link: a slug is only
                sent when it would resolve to a page, and a link to a page that 404s is worse than no
                link at all.
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

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>The offer</h2>
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

        {detail.redemptionUrl ? (
          <p className={styles.cta}>
            <a
              className={styles.ctaLink}
              href={detail.redemptionUrl}
              target="_blank"
              rel="noreferrer noopener"
            >
              Redeem this offer ↗
            </a>
          </p>
        ) : null}

        <p className={styles.note}>
          Nepal Hub stores these details only and never contacts the business on your behalf. Read the
          terms before you go, and check the end date above — offers are removed when they finish.
        </p>
      </article>
    </Container>
  );
}