import type { FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import type { District, Province } from '@/features/reference/api';
import { dealSorts, sortLabels } from '@/features/deals/types';

import styles from './DealFilters.module.css';

/** The filter state as it sits in the URL, where any value may be absent. */
export interface DealFilterValues {
  province: string;
  district: string;
  sort: string;
}

interface DealFiltersProps {
  searchValue: string;
  onSearchValueChange: (value: string) => void;
  values: DealFilterValues;
  provinces: Province[];
  districts: District[];
  districtsLoading: boolean;
  onFilterChange: (key: keyof DealFilterValues, value: string | null) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClear: () => void;
}

/**
 * Search and filters for the offers page.
 *
 * The selects are driven straight from the URL, so choosing one changes the address and the list,
 * and a filtered page can be shared or reloaded without losing its place. The text box is the
 * exception: it waits for the form to be submitted, so a half-typed word does not fire a request
 * per letter.
 *
 * The district list depends on the province, so changing province clears the district. Leaving a
 * district selected under a different province would silently return nothing, which reads to a
 * reader as "no offers there" rather than "your filters disagree".
 *
 * There is no category or verified filter here, unlike the directory: an offer is only ever a
 * discount from a named business, so there is nothing else about it to narrow the list by beyond
 * where it applies.
 */
export function DealFilters({
  searchValue,
  onSearchValueChange,
  values,
  provinces,
  districts,
  districtsLoading,
  onFilterChange,
  onSubmit,
  onClear,
}: DealFiltersProps) {
  return (
    <form className={styles.filters} onSubmit={onSubmit} role="search">
      <div className={styles.searchField}>
        <label className={styles.label} htmlFor="deal-search">
          Search
        </label>
        <input
          className={styles.input}
          id="deal-search"
          name="search"
          type="search"
          value={searchValue}
          placeholder="Title, discount or detail…"
          onChange={(event) => onSearchValueChange(event.target.value)}
        />
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="deal-province">
          Province
        </label>
        <select
          className={styles.input}
          id="deal-province"
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
        <label className={styles.label} htmlFor="deal-district">
          District
        </label>
        <select
          className={styles.input}
          id="deal-district"
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
        <label className={styles.label} htmlFor="deal-sort">
          Order
        </label>
        <select
          className={styles.input}
          id="deal-sort"
          name="sort"
          value={values.sort}
          onChange={(event) => onFilterChange('sort', event.target.value || null)}
        >
          {dealSorts.map((value) => (
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