import { Link } from 'react-router';

import { categoryLabel, describeWhen, formatVenue } from '@/features/events/format';
import type { EventSummary } from '@/features/events/types';

import styles from './EventCard.module.css';

export interface EventCardProps {
  event: EventSummary;
}

/**
 * One event as a row on the events page.
 *
 * The whole card is one link, so clicking the date or the venue asks the same question as clicking
 * the title. "Happening now" is the badge that matters: an event in progress is the only case where
 * the reader has to decide today rather than plan around, so it is called out rather than left to be
 * worked out from the times.
 */
export function EventCard({ event }: EventCardProps) {
  const venue = formatVenue(event.venue, event.districtName);

  return (
    <article className={styles.card}>
      <div className={styles.header}>
        <span className={styles.category}>{categoryLabel(event.category)}</span>
        {event.isOngoing ? <span className={styles.ongoing}>Happening now</span> : null}
      </div>

      <h2 className={styles.title}>
        <Link className={styles.link} to={`/events/${encodeURIComponent(event.slug)}`}>
          {event.title}
        </Link>
      </h2>

      <p className={styles.when}>{describeWhen(event.startsAt, event.endsAt, event.isOngoing)}</p>

      {event.summary ? <p className={styles.summary}>{event.summary}</p> : null}

      <footer className={styles.footer}>
        {venue ? <span className={styles.meta}>{venue}</span> : null}
        {event.organiser ? <span className={styles.meta}>{event.organiser}</span> : null}
        {event.businessName ? <span className={styles.meta}>{event.businessName}</span> : null}
      </footer>
    </article>
  );
}