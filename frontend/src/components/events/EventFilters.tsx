import type { FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import type { District, Province } from '@/features/reference/api';
import { categoryLabels, eventCategories, eventSorts, sortLabels } from '@/features/events/types';

import styles from './EventFilters.module.css';

/** The filter state as it sits in the URL, where any value may be absent. */
export interface EventFilterValues {
  province: string;
  district: string;
  category: string;
  sort: string;
}

interface EventFiltersProps {
  searchValue: string;
  onSearchValueChange: (value: string) => void;
  values: EventFilterValues;
  provinces: Province[];
  districts: District[];
  districtsLoading: boolean;
  onFilterChange: (key: keyof EventFilterValues, value: string | null) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClear: () => void;
}

/**
 * Search and filters for the events page.
 *
 * The selects are driven straight from the URL, so choosing one changes the address and the list,
 * and a filtered calendar can be shared or reloaded without losing its place. The text box waits
 * for the form to be submitted, so a half-typed word does not fire a request per letter.
 *
 * The district list depends on the province, so changing province clears the district. Leaving a
 * district selected under a different province would silently return nothing, which reads to a
 * reader as "nothing on there" rather than "your filters disagree".
 */
export function EventFilters({
  searchValue,
  onSearchValueChange,
  values,
  provinces,
  districts,
  districtsLoading,
  onFilterChange,
  onSubmit,
  onClear,
}: EventFiltersProps) {
  return (
    <form className={styles.filters} onSubmit={onSubmit} role="search">
      <div className={styles.searchField}>
        <label className={styles.label} htmlFor="event-search">
          Search
        </label>
        <input
          className={styles.input}
          id="event-search"
          name="search"
          type="search"
          value={searchValue}
          placeholder="Title, organiser or venue…"
          onChange={(event) => onSearchValueChange(event.target.value)}
        />
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="event-province">
          Province
        </label>
        <select
          className={styles.input}
          id="event-province"
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
        <label className={styles.label} htmlFor="event-district">
          District
        </label>
        <select
          className={styles.input}
          id="event-district"
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
        <label className={styles.label} htmlFor="event-category">
          Category
        </label>
        <select
          className={styles.input}
          id="event-category"
          name="category"
          value={values.category}
          onChange={(event) => onFilterChange('category', event.target.value || null)}
        >
          <option value="">All categories</option>
          {eventCategories.map((value) => (
            <option key={value} value={value}>
              {categoryLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="event-sort">
          Order
        </label>
        <select
          className={styles.input}
          id="event-sort"
          name="sort"
          value={values.sort}
          onChange={(event) => onFilterChange('sort', event.target.value || null)}
        >
          {eventSorts.map((value) => (
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