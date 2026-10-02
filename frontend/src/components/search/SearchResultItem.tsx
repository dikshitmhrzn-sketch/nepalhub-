import { Link } from 'react-router';

import { resultHref, resultMeta } from '@/features/search/format';
import type { SearchResult } from '@/features/search/types';
import { moduleLabels } from '@/features/search/types';

import styles from './SearchResultItem.module.css';

export interface SearchResultItemProps {
  result: SearchResult;
  /** The words the reader typed, so each result can show where it matched. */
  query: string;
}

/**
 * One result line.
 *
 * The title is the link, and it is the only one. A result has exactly one destination, and the
 * module tag, the place and the dates are facts about that destination rather than places of their
 * own — a link nested inside the card's own link is not something HTML can express anyway.
 *
 * A result from a module this build does not recognise renders as plain text rather than as a link
 * to nowhere, which is what `resultHref` returning null means.
 */
export function SearchResultItem({ result, query }: SearchResultItemProps) {
  const href = resultHref(result);
  const meta = resultMeta(result);
  const title = <Highlighted text={result.title} term={query} />;

  return (
    <article className={styles.item}>
      <p className={styles.module}>{moduleLabels[result.module]}</p>

      <h3 className={styles.title}>
        {href ? (
          <Link className={styles.link} to={href}>
            {title}
          </Link>
        ) : (
          title
        )}
      </h3>

      {result.summary ? (
        <p className={styles.summary}>
          <Highlighted text={result.summary} term={query} />
        </p>
      ) : null}

      {meta ? <p className={styles.meta}>{meta}</p> : null}
    </article>
  );
}

/**
 * Marks the part of a string that matched, so a reader can see why a result is on the page.
 *
 * Split on a case-insensitive index and rendered as separate nodes rather than by injecting markup:
 * every piece is a text node React escapes, so a row whose title contains angle brackets cannot turn
 * the results page into markup.
 *
 * The comparison is done on the original string without normalising, which means a search for
 * "café" will not highlight an "e-acute" the server matched through its accent-insensitive
 * collation. Highlighting is a nicety; refusing to show the result would be a bug, so the asymmetry
 * is accepted in that direction.
 */
function Highlighted({ text, term }: { text: string; term: string }) {
  const needle = term.trim();

  if (needle.length < 2) {
    return text;
  }

  const index = text.toLowerCase().indexOf(needle.toLowerCase());

  if (index < 0) {
    return text;
  }

  return (
    <>
      {text.slice(0, index)}
      <mark className={styles.mark}>{text.slice(index, index + needle.length)}</mark>
      {text.slice(index + needle.length)}
    </>
  );
}