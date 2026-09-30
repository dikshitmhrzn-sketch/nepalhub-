import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { CalculatorCard } from '@/components/calculators/CalculatorCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useCalculatorList } from '@/features/calculators/useCalculatorList';
import { calculatorCategories } from '@/features/calculators/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './ToolsPage.module.css';

const PAGE_SIZE = 12;

export function ToolsPage() {
  // The URL owns paging, search and category, so a filtered catalogue can be shared
  // or reloaded without losing its place.
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const search = searchParams.get('search') ?? '';
  const requestedCategory = searchParams.get('category');
  const category = calculatorCategories.find((value) => value === requestedCategory);

  const [searchInput, setSearchInput] = useState(search);
  const list = useCalculatorList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    category,
  });

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

  function handleSearchSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    updateParams({ page: null, search: searchInput.trim() || null });
  }

  function handleClear() {
    setSearchInput('');
    updateParams({ page: null, search: null, category: null });
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Tools and calculators</h1>
        <p className={styles.subtitle}>
          Everyday calculations for money, study, health and more. Everything runs on the
          server, so the figures come from one audited formula rather than your browser.
        </p>
      </header>

      <form className={styles.filters} onSubmit={handleSearchSubmit} role="search">
        <div className={styles.searchField}>
          <label className={styles.label} htmlFor="tool-search">
            Search tools
          </label>
          <input
            className={styles.input}
            id="tool-search"
            name="search"
            type="search"
            value={searchInput}
            placeholder="Loan, interest, percentage…"
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </div>

        <div className={styles.categoryField}>
          <label className={styles.label} htmlFor="tool-category">
            Category
          </label>
          <select
            className={styles.input}
            id="tool-category"
            name="category"
            value={requestedCategory ?? ''}
            onChange={(event) =>
              updateParams({ page: null, category: event.target.value || null })
            }
          >
            <option value="">All categories</option>
            {calculatorCategories.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.filterActions}>
          <Button type="submit" variant="secondary" size="small">
            Search
          </Button>
          <Button type="button" variant="ghost" size="small" onClick={handleClear}>
            Clear
          </Button>
        </div>
      </form>

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading the tools…" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the tools"
          description={getErrorMessage(list.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={list.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      {list.status === 'success' && list.data.items.length === 0 ? (
        <StateMessage
          tone="empty"
          title="No tools match that search"
          description="Try a different word, or clear the filters to see everything."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.grid}>
            {list.data.items.map((calculator) => (
              <li key={calculator.slug}>
                <CalculatorCard calculator={calculator} />
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="tools"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}
