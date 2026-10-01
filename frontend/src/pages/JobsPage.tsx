import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { JobCard } from '@/components/jobs/JobCard';
import { JobFilters, type JobFilterValues } from '@/components/jobs/JobFilters';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useJobList } from '@/features/jobs/useJobs';
import {
  employmentTypes,
  jobCategories,
  jobSorts,
  workModes,
} from '@/features/jobs/types';
import { useReferenceData } from '@/features/reference/useReferenceData';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './JobsPage.module.css';

const PAGE_SIZE = 12;

export function JobsPage() {
  // The URL owns paging, search, filters and order, so a filtered board can be shared or
  // reloaded without losing its place. Each value is checked against the known list before it
  // is sent, so a hand-edited address falls back to the default rather than being refused by
  // the API.
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const search = searchParams.get('search') ?? '';

  const requestedProvince = searchParams.get('province');
  const requestedDistrict = searchParams.get('district');
  const requestedCategory = searchParams.get('category');
  const requestedEmploymentType = searchParams.get('employmentType');
  const requestedWorkMode = searchParams.get('workMode');
  const requestedSort = searchParams.get('sort');

  const reference = useReferenceData();
  const provinces = reference.data?.provinces ?? [];

  // The province and district are reference codes rather than enum members, so they are
  // validated against what the reference endpoints returned rather than against a hard-coded
  // list that would drift from the database.
  const province = provinces.find((item) => item.code === requestedProvince);
  const knownDistricts = Object.values(reference.data?.districtsByProvince ?? {}).flat();
  const district = knownDistricts.find((item) => item.code === requestedDistrict);

  const category = jobCategories.find((value) => value === requestedCategory);
  const employmentType = employmentTypes.find((value) => value === requestedEmploymentType);
  const workMode = workModes.find((value) => value === requestedWorkMode);
  const sort = jobSorts.find((value) => value === requestedSort);

  const [searchInput, setSearchInput] = useState(search);
  const list = useJobList({
    page,
    pageSize: PAGE_SIZE,
    search: search || undefined,
    province: province?.code,
    district: district?.code,
    category,
    employmentType,
    workMode,
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

  function handleFilterChange(key: keyof JobFilterValues, value: string | null) {
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
      employmentType: null,
      workMode: null,
      sort: null,
    });
  }

  const districts = province ? (reference.data?.districtsByProvince[province.code] ?? []) : [];

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Jobs</h1>
        <p className={styles.subtitle}>
          Openings across Nepal, from the districts to the national employers. Each posting is
          checked by a person, and it leaves the board on the day after its closing date.
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

      <JobFilters
        searchValue={searchInput}
        onSearchValueChange={setSearchInput}
        values={{
          province: province?.code ?? '',
          district: district?.code ?? '',
          category: category ?? '',
          employmentType: employmentType ?? '',
          workMode: workMode ?? '',
          sort: sort ?? 'Newest',
        }}
        provinces={provinces}
        districts={districts}
        districtsLoading={reference.status === 'loading'}
        onFilterChange={handleFilterChange}
        onSubmit={handleSearchSubmit}
        onClear={handleClear}
      />

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading the board…" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the job board"
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
          description="Try a different word, or clear the filters to see every posting."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.grid}>
            {list.data.items.map((job) => (
              <li key={job.id}>
                <JobCard job={job} />
              </li>
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="postings"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}