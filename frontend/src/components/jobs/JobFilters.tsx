import type { FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import type { District, Province } from '@/features/reference/api';
import {
  categoryLabels,
  employmentTypeLabels,
  jobCategories,
  jobSorts,
  sortLabels,
  employmentTypes,
  workModeLabels,
  workModes,
} from '@/features/jobs/types';

import styles from './JobFilters.module.css';

/** The filter state as it sits in the URL, where any value may be absent. */
export interface JobFilterValues {
  province: string;
  district: string;
  category: string;
  employmentType: string;
  workMode: string;
  sort: string;
}

interface JobFiltersProps {
  searchValue: string;
  onSearchValueChange: (value: string) => void;
  values: JobFilterValues;
  provinces: Province[];
  districts: District[];
  districtsLoading: boolean;
  onFilterChange: (key: keyof JobFilterValues, value: string | null) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClear: () => void;
}

/**
 * Search and filters for the public job board.
 *
 * The selects are driven straight from the URL, so choosing one changes the address and the
 * list, and a filtered board can be shared or reloaded without losing its place. The text box
 * is the exception: it waits for the form to be submitted, so a half-typed word does not fire
 * a request per letter.
 *
 * The district list depends on the province, so changing province clears the district. Leaving
 * a district selected under a different province would silently return nothing, which reads to
 * a reader as "no jobs there" rather than "your filters disagree".
 */
export function JobFilters({
  searchValue,
  onSearchValueChange,
  values,
  provinces,
  districts,
  districtsLoading,
  onFilterChange,
  onSubmit,
  onClear,
}: JobFiltersProps) {
  return (
    <form className={styles.filters} onSubmit={onSubmit} role="search">
      <div className={styles.searchField}>
        <label className={styles.label} htmlFor="job-search">
          Search
        </label>
        <input
          className={styles.input}
          id="job-search"
          name="search"
          type="search"
          value={searchValue}
          placeholder="Title, employer or keyword…"
          onChange={(event) => onSearchValueChange(event.target.value)}
        />
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-province">
          Province
        </label>
        <select
          className={styles.input}
          id="job-province"
          name="province"
          value={values.province}
          onChange={(event) => {
            onFilterChange('district', null);
            onFilterChange('province', event.target.value || null);
          }}
        >
          <option value="">All provinces</option>
          {provinces.map((province) => (
            <option key={province.code} value={province.code}>
              {province.name}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-district">
          District
        </label>
        <select
          className={styles.input}
          id="job-district"
          name="district"
          value={values.district}
          disabled={!values.province || districtsLoading}
          onChange={(event) => onFilterChange('district', event.target.value || null)}
        >
          <option value="">
            {values.province ? 'All districts' : 'Choose a province first'}
          </option>
          {districts.map((district) => (
            <option key={district.code} value={district.code}>
              {district.name}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-category">
          Sector
        </label>
        <select
          className={styles.input}
          id="job-category"
          name="category"
          value={values.category}
          onChange={(event) => onFilterChange('category', event.target.value || null)}
        >
          <option value="">All sectors</option>
          {jobCategories.map((value) => (
            <option key={value} value={value}>
              {categoryLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-employment-type">
          Employment
        </label>
        <select
          className={styles.input}
          id="job-employment-type"
          name="employmentType"
          value={values.employmentType}
          onChange={(event) => onFilterChange('employmentType', event.target.value || null)}
        >
          <option value="">Any employment</option>
          {employmentTypes.map((value) => (
            <option key={value} value={value}>
              {employmentTypeLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-work-mode">
          Workplace
        </label>
        <select
          className={styles.input}
          id="job-work-mode"
          name="workMode"
          value={values.workMode}
          onChange={(event) => onFilterChange('workMode', event.target.value || null)}
        >
          <option value="">Any workplace</option>
          {workModes.map((value) => (
            <option key={value} value={value}>
              {workModeLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="job-sort">
          Order
        </label>
        <select
          className={styles.input}
          id="job-sort"
          name="sort"
          value={values.sort}
          onChange={(event) => onFilterChange('sort', event.target.value || null)}
        >
          {jobSorts.map((value) => (
            <option key={value} value={value}>
              {sortLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.actions}>
        <Button type="submit" variant="secondary" size="small">
          Search
        </Button>
        <Button type="button" variant="ghost" size="small" onClick={onClear}>
          Clear
        </Button>
      </div>
    </form>
  );
}