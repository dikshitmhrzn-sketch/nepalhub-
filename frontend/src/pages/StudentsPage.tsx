import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import {
  ResourceFilters,
  type ResourceFilterValues,
} from '@/components/students/ResourceFilters';
import { ResourceCard } from '@/components/students/ResourceCard';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useResourceList } from '@/features/students/useResources';
import {
  educationLevels,
  resourceCategories,
  resourceSorts,
  resourceTypes,
} from '@/features/students/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './StudentsPage.module.css';

const PAGE_SIZE = 12;

export function StudentsPage() {
  // The URL owns paging, search, filters and order, so a filtered catalogue can be
  // shared or reloaded without losing its place. Each value is checked against the
  // known list before it is sent, so a hand-edited address falls back to the default
  // rather than being refused by the API.
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const search = searchParams.get('search') ?? '';

  const requestedType = searchParams.get('type');
  const requestedCategory = searchParams.get('category');
  const requestedLevel = searchParams.get('level');
  const requestedSort = searchParams.get('sort');

  const type = resourceTypes.find((value) => value === requestedType);
  const category = resourceCategories.find((value) => value === requestedCategory);
  const level = educationLevels.find((value) => value === requestedLevel);
  const sort = resourceSorts.find((value) => value === requestedSort);

  const [searchInput, setSearchInput] = useState(search);
  const list = useResourceList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    type,
    category,
    level,
    sort,
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

  function handleFilterChange(key: keyof ResourceFilterValues, value: string | null) {
    updateParams({ page: null, [key]: value });
  }

  function handleClear() {
    setSearchInput('');
    updateParams({
      page: null,
      search: null,
      type: null,
      category: null,
      level: null,
      sort: null,
    });
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Student Hub</h1>
        <p className={styles.subtitle}>
          Study notes, past question papers and scholarships, kept in one catalogue. Everything
          here has been checked by a person, and a scholarship disappears on the day its
          applications close.
        </p>
      </header>

      <ResourceFilters
        searchValue={searchInput}
        onSearchValueChange={setSearchInput}
        values={{
          type: type ?? '',
          category: category ?? '',
          level: level ?? '',
          sort: sort ?? 'Newest',
        }}
        onFilterChange={handleFilterChange}
        onSubmit={handleSearchSubmit}
        onClear={handleClear}
      />

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading the catalogue…" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the catalogue"
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
          title="Nothing matches that search"
          description="Try a different word, or clear the filters to see everything."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.grid}>
            {list.data.items.map((resource) => (
              <li key={resource.id}>
                <ResourceCard resource={resource} />
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="resources"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}
