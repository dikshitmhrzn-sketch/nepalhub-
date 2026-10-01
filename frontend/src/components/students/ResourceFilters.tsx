import type { FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import {
  categoryLabels,
  educationLevels,
  levelLabels,
  resourceCategories,
  resourceSorts,
  resourceTypes,
  sortLabels,
  typeLabels,
} from '@/features/students/types';

import styles from './ResourceFilters.module.css';

/** The filter state as it sits in the URL, where a value may be absent. */
export interface ResourceFilterValues {
  type: string;
  category: string;
  level: string;
  sort: string;
}

interface ResourceFiltersProps {
  searchValue: string;
  onSearchValueChange: (value: string) => void;
  values: ResourceFilterValues;
  onFilterChange: (key: keyof ResourceFilterValues, value: string | null) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onClear: () => void;
}

/**
 * Search and filters for the public catalogue. The selects are driven straight from the
 * URL, so choosing one changes the address and the list, and a filtered catalogue can be
 * shared or reloaded without losing its place. The text box is the exception: it waits
 * for the form to be submitted, so a half-typed word does not fire a request per letter.
 */
export function ResourceFilters({
  searchValue,
  onSearchValueChange,
  values,
  onFilterChange,
  onSubmit,
  onClear,
}: ResourceFiltersProps) {
  return (
    <form className={styles.filters} onSubmit={onSubmit} role="search">
      <div className={styles.searchField}>
        <label className={styles.label} htmlFor="resource-search">
          Search
        </label>
        <input
          className={styles.input}
          id="resource-search"
          name="search"
          type="search"
          value={searchValue}
          placeholder="Notes, papers, scholarships…"
          onChange={(event) => onSearchValueChange(event.target.value)}
        />
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-type">
          Type
        </label>
        <select
          className={styles.input}
          id="resource-type"
          name="type"
          value={values.type}
          onChange={(event) => onFilterChange('type', event.target.value || null)}
        >
          <option value="">All types</option>
          {resourceTypes.map((value) => (
            <option key={value} value={value}>
              {typeLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-category">
          Subject
        </label>
        <select
          className={styles.input}
          id="resource-category"
          name="category"
          value={values.category}
          onChange={(event) => onFilterChange('category', event.target.value || null)}
        >
          <option value="">All subjects</option>
          {resourceCategories.map((value) => (
            <option key={value} value={value}>
              {categoryLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-level">
          Level
        </label>
        <select
          className={styles.input}
          id="resource-level"
          name="level"
          value={values.level}
          onChange={(event) => onFilterChange('level', event.target.value || null)}
        >
          <option value="">Any level</option>
          {educationLevels.map((value) => (
            <option key={value} value={value}>
              {levelLabels[value]}
            </option>
          ))}
        </select>
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="resource-sort">
          Order
        </label>
        <select
          className={styles.input}
          id="resource-sort"
          name="sort"
          value={values.sort}
          onChange={(event) => onFilterChange('sort', event.target.value || null)}
        >
          {resourceSorts.map((value) => (
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
