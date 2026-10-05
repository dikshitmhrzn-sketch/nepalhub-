import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { BusinessCard } from '@/components/businesses/BusinessCard';
import { BusinessFilters, type BusinessFilterValues } from '@/components/businesses/BusinessFilters';
import { FeaturedStrip } from '@/components/businesses/FeaturedStrip';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useBusinessList } from '@/features/businesses/useBusinesses';
import { businessCategories, businessSorts } from '@/features/businesses/types';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './BusinessesPage.module.css';

const PAGE_SIZE = 12;

export function BusinessesPage() {
  // The URL owns paging, search, filters and order, so a filtered directory can be shared or
  // reloaded without losing its place. Each value is checked against the known list before it is
  // sent, so a hand-edited address falls back to the default rather than being refused by the API.
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const search = searchParams.get('search') ?? '';

  const requestedProvince = searchParams.get('province');
  const requestedDistrict = searchParams.get('district');
  const requestedCategory = searchParams.get('category');
  const requestedSort = searchParams.get('sort');
  const requestedVerified = searchParams.get('verified');

  const reference = useReferenceData();
  const provinces = reference.data?.provinces ?? [];

  const province = provinces.find((item) => item.code === requestedProvince);
  const knownDistricts = Object.values(reference.data?.districtsByProvince ?? {}).flat();
  const district = knownDistricts.find((item) => item.code === requestedDistrict);

  const category = businessCategories.find((value) => value === requestedCategory);
  const sort = businessSorts.find((value) => value === requestedSort);

  // "true" and "false" are the two states the filter offers. Anything else in the address is
  // treated as no filter rather than being passed on, so a stray value cannot ask the API for a
  // filter the reader did not choose.
  const verified =
    requestedVerified === 'true' ? true : requestedVerified === 'false' ? false : undefined;

  const [searchInput, setSearchInput] = useState(search);
  const list = useBusinessList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    province: province?.code,
    district: district?.code,
    category,
    verified,
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

  function handleFilterChange(key: keyof BusinessFilterValues, value: string | null) {
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
      verified: null,
      sort: null,
    });
  }

  const districts = province ? (reference.data?.districtsByProvince[province.code] ?? []) : [];

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Businesses</h1>
        <p className={styles.subtitle}>
          Useful local businesses and services across Nepal, from a place to eat to someone to fix
          your computer. Every listing is written and checked by our staff.
        </p>
      </header>

      {/*
        The paid placements sit between the introduction and the directory: labelled, at most
        six, alphabetical. A strip nobody holds — or one that could not be fetched — renders
        nothing at all, so the directory below is exactly as it was.
      */}
      <FeaturedStrip />

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

      <BusinessFilters
        searchValue={searchInput}
        onSearchValueChange={setSearchInput}
        values={{
          province: province?.code ?? '',
          district: district?.code ?? '',
          category: category ?? '',
          verified: requestedVerified ?? '',
          sort: sort ?? 'Name',
        }}
        provinces={provinces}
        districts={districts}
        districtsLoading={reference.status === 'loading'}
        onFilterChange={handleFilterChange}
        onSubmit={handleSearchSubmit}
        onClear={handleClear}
      />

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading the directory…" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the directory"
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
          description="Try a different word, or clear the filters to see every listing."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.grid}>
            {list.data.items.map((business) => (
              <li key={business.id}>
                <BusinessCard business={business} />
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="listings"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}
