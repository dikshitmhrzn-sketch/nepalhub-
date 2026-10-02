import { moduleLabels, searchModules, type SearchModule } from '@/features/search/types';

import styles from './SearchModuleTabs.module.css';

export interface SearchModuleTabsProps {
  value: SearchModule;
  onChange: (module: SearchModule) => void;
  /** Each tab's own hit count, so a reader can see where to look before switching. */
  counts?: Partial<Record<SearchModule, number>>;
  disabled?: boolean;
}

/**
 * The module filter.
 *
 * Rendered as links rather than buttons and kept in step with the URL by the page, because a search
 * is a thing a reader shares and reloads. The counts come from the current page's own groups, so
 * they are capped the same way the totals are: a tab saying 0 means nothing matched in that module
 * for this term, which is a claim the API can support.
 */
export function SearchModuleTabs({ value, onChange, counts, disabled }: SearchModuleTabsProps) {
  return (
    <div className={styles.tabs} role="group" aria-label="Limit search to one part of the site">
      {searchModules.map((module) => {
        const count = counts?.[module];
        const active = module === value;

        return (
          <button
            key={module}
            type="button"
            className={active ? `${styles.tab} ${styles.tabActive}` : styles.tab}
            aria-pressed={active}
            disabled={disabled}
            onClick={() => onChange(module)}
          >
            {moduleLabels[module]}
            {typeof count === 'number' && count > 0 ? (
              <span className={styles.count}>{count}</span>
            ) : null}
          </button>
        );
      })}
    </div>
  );
}