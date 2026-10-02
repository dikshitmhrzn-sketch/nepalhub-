import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { SearchModuleTabs } from '@/components/search/SearchModuleTabs';
import { SearchPagination } from '@/components/search/SearchPagination';
import { SearchResultItem } from '@/components/search/SearchResultItem';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { countLabel } from '@/features/search/format';
import { useSearchResults } from '@/features/search/useSearch';
import {
  minQueryLength,
  searchLimits,
  searchModules,
  type SearchGroup,
  type SearchModule,
} from '@/features/search/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './SearchPage.module.css';

/**
 * The search results page.
 *
 * The URL owns the term, the module and the page number, so a search is a thing a reader can share,
 * reload and come back to. Both values are checked against what is known before being sent, so a
 * hand-edited address falls back to a default rather than drawing an error from the API — `?module=Jobs`
 * works, `?module=nonsense` reads as Everything.
 *
 * The page number is reset whenever the term or the module changes. Paging is a position within one
 * search, so keeping `page=4` after the reader has narrowed it would land them past the end of a
 * list that now has two pages, and the page would look empty rather than wrong.
 */
export function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = searchParams.get('q')?.trim() ?? '';
  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);

  const requestedModule = searchParams.get('module');
  const module = searchModules.find((value) => value === requestedModule) ?? 'All';

  const [input, setInput] = useState(query);

  // The header box navigates to /search?q=… rather than submitting this form, so arriving here with a
  // different term in the address bar does not remount the page and would otherwise leave the box
  // showing the previous search while the results show the new one. Adjusting during render, on the
  // terms actually differing, is what React recommends for state that has to follow a prop: an effect
  // would paint one frame with the old term still in the box.
  const [shownQuery, setShownQuery] = useState(query);

  if (query !== shownQuery) {
    setShownQuery(query);
    setInput(query);
  }

  const results = useSearchResults(
    query.length >= minQueryLength ? { q: query, page, module } : null,
  );

  function updateParams(changes: Record<string, string | null>) {
    const next = new URLSearchParams(searchParams);

    for (const [key, value] of Object.entries(changes)) {
      if (value) {
        next.set(key, value);
      } else {
        next.delete(key);
      }
    }

    setSearchParams(next, { replace: true });
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const term = input.trim();

    updateParams({
      q: term.length >= minQueryLength ? term : null,
      page: null,
    });
  }

  function handleModuleChange(next: SearchModule) {
    updateParams({ module: next === 'All' ? null : next, page: null });
  }

  function handleClear() {
    setInput('');
    updateParams({ q: null, module: null, page: null });
  }

  // Only what the reader has typed so far, truncated at the server's limit, so the box cannot hold
  // more than a search could ever contain.
  const shownTerm = query.length > searchLimits.maxQueryLength ? query.slice(0, searchLimits.maxQueryLength) : query;

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Search</h1>
        <p className={styles.subtitle}>
          One box for jobs, offers, events, businesses, student resources and tools across Nepal Hub.
        </p>
      </header>

      <form className={styles.form} role="search" onSubmit={handleSubmit}>
        <label className={styles.label} htmlFor="search-term">
          Search Nepal Hub
        </label>

        <input
          id="search-term"
          className={styles.input}
          type="search"
          name="q"
          value={input}
          placeholder="What are you looking for?"
          maxLength={searchLimits.maxQueryLength}
          autoComplete="off"
          aria-describedby="search-hint"
          onChange={(event) => setInput(event.target.value)}
        />

        <button className={styles.submit} type="submit">
          Search
        </button>

        <p className={styles.hint} id="search-hint">
          At least {minQueryLength} characters. Wildcards like % and _ are matched literally.
        </p>
      </form>

      {query.length > 0 && query.length < minQueryLength ? (
        <StateMessage
          tone="empty"
          title="Keep typing"
          description={`A search needs at least ${minQueryLength} characters.`}
        />
      ) : null}

      {query.length >= minQueryLength ? (
        <SearchModuleTabs
          value={module}
          onChange={handleModuleChange}
          counts={tabCounts(results.status === 'success' ? results.data : null)}
        />
      ) : null}

      {query.length >= minQueryLength && results.status === 'loading' ? (
        <StateMessage tone="loading" title="Searching…" />
      ) : null}

      {query.length >= minQueryLength && results.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not run that search"
          description={getErrorMessage(results.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={results.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {query.length >= minQueryLength && results.status === 'success' ? (
        results.data.totalCount === 0 ? (
          <StateMessage
            tone="empty"
            title={`Nothing matches “${shownTerm}”`}
            description="Try a shorter or different word — fewer characters match more."
            action={
              <Button variant="secondary" size="small" onClick={handleClear}>
                Clear the search
              </Button>
            }
          />
        ) : (
          <>
            <p className={styles.count}>
              {countLabel(results.data.totalCount, results.data.truncated)}
              {results.data.truncated ? (
                <span className={styles.countNote}>
                  {' '}
                  Each part of the site is searched up to a fixed number of rows, so this is not the
                  full count.
                </span>
              ) : null}
            </p>

            {results.data.groups.map((group) => (
              <section className={styles.group} key={group.module} aria-labelledby={`group-${group.module}`}>
                <h2 className={styles.groupTitle} id={`group-${group.module}`}>
                  {group.label}
                  <span className={styles.groupCount}>{group.totalCount}</span>
                </h2>

                <div>
                  {group.items.map((item) => (
                    <SearchResultItem key={`${item.module}:${item.referenceId}`} result={item} query={query} />
                  ))}
                </div>

                {group.totalCount > group.items.length ? (
                  <p className={styles.more}>
                    Showing the strongest {group.items.length} of {group.totalCount} in this section.
                  </p>
                ) : null}
              </section>
            ))}

            <SearchPagination
              page={results.data.page}
              totalPages={results.data.totalPages}
              hasPrevious={results.data.hasPrevious}
              hasNext={results.data.hasNext}
              onPageChange={(next) => updateParams({ page: String(next) })}
            />
          </>
        )
      ) : null}

      {query.length === 0 ? (
        <StateMessage
          tone="empty"
          title="What are you looking for?"
          description="Search for a job, an offer, an event, a business, a student resource or a tool."
        />
      ) : null}
    </Container>
  );
}

/**
 * The per-tab counts.
 *
 * Read off the groups the response actually returned, so a tab claims only what the response can
 * stand behind. Under a single-module filter every other tab reads zero rather than being hidden,
 * because leaving the tab visible and honestly empty is more use to a reader than removing it and
 * making the filter look like it moved the page.
 */
function tabCounts(data: { totalCount: number; groups: SearchGroup[] } | null) {
  if (!data) {
    return undefined;
  }

  const counts: Partial<Record<SearchModule, number>> = { All: data.totalCount };

  for (const group of data.groups) {
    counts[group.module] = group.totalCount;
  }

  return counts;
}