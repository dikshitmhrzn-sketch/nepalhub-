import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { DealCard } from '@/components/deals/DealCard';
import { DealFilters, type DealFilterValues } from '@/components/deals/DealFilters';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useDealList } from '@/features/deals/useDeals';
import { dealSorts } from '@/features/deals/types';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './DealsPage.module.css';

const PAGE_SIZE = 12;

/**
 * The offers page.
 *
 * The URL owns paging, search, place and order, so a filtered page can be shared or reloaded
 * without losing its place. Every value is checked against the known list before it is sent, so a
 * hand-edited address falls back to the default rather than being refused by the API.
 *
 * Sorting defaults to EndingSoon because that is what a reader of an offers page is for: the deals
 * about to expire are the ones worth acting on this week.
 */
export function DealsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const search = searchParams.get('search') ?? '';

  const requestedProvince = searchParams.get('province');
  const requestedDistrict = searchParams.get('district');
  const requestedSort = searchParams.get('sort');

  const reference = useReferenceData();
  const provinces = reference.data?.provinces ?? [];

  const province = provinces.find((item) => item.code === requestedProvince);
  const knownDistricts = Object.values(reference.data?.districtsByProvince ?? {}).flat();
  const district = knownDistricts.find((item) => item.code === requestedDistrict);

  const sort = dealSorts.find((value) => value === requestedSort);

  const [searchInput, setSearchInput] = useState(search);
  const list = useDealList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    province: province?.code,
    district: district?.code,
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

  function handleFilterChange(key: keyof DealFilterValues, value: string | null) {
    updateParams({ page: null, [key]: value });
  }

  function handleClear() {
    setSearchInput('');
    updateParams({
      page: null,
      search: null,
      province: null,
      district: null,
      sort: null,
    });
  }

  const districts = province ? (reference.data?.districtsByProvince[province.code] ?? []) : [];

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Offers</h1>
        <p className={styles.subtitle}>
          Discounts and offers from businesses and organisations around Nepal. Every offer has an
          end date, so anything you see here is still running unless it says otherwise.
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

      <DealFilters
        searchValue={searchInput}
        onSearchValueChange={setSearchInput}
        values={{
          province: province?.code ?? '',
          district: district?.code ?? '',
          sort: sort ?? 'EndingSoon',
        }}
        provinces={provinces}
        districts={districts}
        districtsLoading={reference.status === 'loading'}
        onFilterChange={handleFilterChange}
        onSubmit={handleSearchSubmit}
        onClear={handleClear}
      />

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading the offers…" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the offers"
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
          description="Try a different word, or clear the filters to see every offer."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.grid}>
            {list.data.items.map((deal) => (
              <li key={deal.id}>
                <DealCard deal={deal} />
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="offers"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}