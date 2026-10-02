import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { EventCard } from '@/components/events/EventCard';
import { EventFilters, type EventFilterValues } from '@/components/events/EventFilters';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useEventList } from '@/features/events/useEvents';
import { eventCategories, eventSorts } from '@/features/events/types';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './EventsPage.module.css';

const PAGE_SIZE = 12;

/**
 * The events page.
 *
 * The URL owns paging, search, place, category and order, so a filtered calendar can be shared or
 * reloaded without losing its place. Every value is checked against the known list before it is sent,
 * so a hand-edited address falls back to the default rather than being refused by the API.
 *
 * Sorting defaults to Soonest because the question a reader arrives with is what to do this week.
 * Finished events are not in this list at all: the API drops them, so there is no filter for it and
 * nothing to switch off.
 */
export function EventsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const search = searchParams.get('search') ?? '';

  const requestedProvince = searchParams.get('province');
  const requestedDistrict = searchParams.get('district');
  const requestedCategory = searchParams.get('category');
  const requestedSort = searchParams.get('sort');

  const reference = useReferenceData();
  const provinces = reference.data?.provinces ?? [];

  const province = provinces.find((item) => item.code === requestedProvince);
  const knownDistricts = Object.values(reference.data?.districtsByProvince ?? {}).flat();
  const district = knownDistricts.find((item) => item.code === requestedDistrict);

  const category = eventCategories.find((value) => value === requestedCategory);
  const sort = eventSorts.find((value) => value === requestedSort);

  const [searchInput, setSearchInput] = useState(search);
  const list = useEventList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    province: province?.code,
    district: district?.code,
    category,
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

  function handleFilterChange(key: keyof EventFilterValues, value: string | null) {
    updateParams({ page: null, [key]: value });
  }

  function handleClear() {
    setSearchInput('');
    updateParams({
      page: null,
      search: null,
      province: null,
      district: null,
      category: null,
      sort: null,
    });
  }

  const districts = province ? (reference.data?.districtsByProvince[province.code] ?? []) : [];

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Events</h1>
        <p className={styles.subtitle}>
          Fairs, festivals, training, matches and gatherings happening across Nepal. Times are Nepal
          Standard Time, and an event stays on this list until it has finished.
        </p>
      </header>

      {reference.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the location filters"
          description={getErrorMessage(reference.error, 'Please try again in a moment.')}
          action={
            <Button variant="secondary" size="small" onClick={reference.reload}>
              Try again
            </Button>
          }
        />
      ) : null}

      <EventFilters
        searchValue={searchInput}
        onSearchValueChange={setSearchInput}
        values={{
          province: province?.code ?? '',
          district: district?.code ?? '',
          category: category ?? '',
          sort: sort ?? 'Soonest',
        }}
        provinces={provinces}
        districts={districts}
        districtsLoading={reference.status === 'loading'}
        onFilterChange={handleFilterChange}
        onSubmit={handleSearchSubmit}
        onClear={handleClear}
      />

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading the events…" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the events"
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
          description="Try a different word, or clear the filters to see every event."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.grid}>
            {list.data.items.map((event) => (
              <li key={event.id}>
                <EventCard event={event} />
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="events"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}