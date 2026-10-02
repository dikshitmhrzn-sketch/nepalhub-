import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router';

import { minQueryLength, searchLimits } from '@/features/search/types';

import styles from './HeaderSearch.module.css';

/**
 * The search box in the header.
 *
 * A plain form that navigates to `/search` on submit. It deliberately does not search as the reader
 * types: one box that fires a request per keystroke would send six queries before the word is
 * finished, spend the search quota while somebody is still typing, and race its own responses. The
 * results page owns the request, and the URL owns the term, so a search is still a thing that can be
 * shared, reloaded and reached with the back button.
 */
export function HeaderSearch() {
  const navigate = useNavigate();
  const location = useLocation();

  // Seeded from the address bar so arriving on a shared search link shows the term that produced it.
  const [term, setTerm] = useState(() => new URLSearchParams(location.search).get('q') ?? '');

  const trimmed = term.trim();
  const tooShort = trimmed.length > 0 && trimmed.length < minQueryLength;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    // The shell hides this box on the search page itself, so submitting always moves to a new page
    // and the back button returns to wherever the reader came from, as it would for any other page.
    navigate(trimmed.length >= minQueryLength ? `/search?q=${encodeURIComponent(trimmed)}` : '/search');
  }

  return (
    <form className={styles.form} role="search" onSubmit={handleSubmit}>
      <label className={styles.label} htmlFor="site-search">
        Search Nepal Hub
      </label>

      <input
        id="site-search"
        className={styles.input}
        type="search"
        name="q"
        value={term}
        placeholder="Search jobs, offers, events…"
        maxLength={searchLimits.maxQueryLength}
        autoComplete="off"
        // Announced only once the reader has typed something, so the hint is not read out on a page
        // where they have not looked at the box at all.
        aria-describedby={tooShort ? 'site-search-hint' : undefined}
        onChange={(event) => setTerm(event.target.value)}
      />

      <button className={styles.submit} type="submit">
        Search
      </button>

      {tooShort ? (
        <p className={styles.hint} id="site-search-hint">
          Enter at least {minQueryLength} characters.
        </p>
      ) : null}
    </form>
  );
}