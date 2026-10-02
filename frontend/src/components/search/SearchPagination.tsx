import { Button } from '@/components/ui/Button';

import styles from './SearchPagination.module.css';

export interface SearchPaginationProps {
  page: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
  onPageChange: (page: number) => void;
}

/**
 * Previous/next paging for search, without the count the shared `Pagination` prints.
 *
 * That component's status line reads "Page 1 of 3 · 47 results", and search cannot support the second
 * half of it: the API ranks a bounded number of candidates per module rather than counting every row
 * that matched, so 47 is a statement about the strongest matches and not about the site. Showing the
 * number twice, once qualified and once as though it were exact, would undo the qualification. The
 * count is on the results themselves; this says only where in the list the reader is.
 *
 * The flags come from the response rather than being compared here, so the server's own answer to
 * "is there another page" is the one that is honoured.
 */
export function SearchPagination({
  page,
  totalPages,
  hasPrevious,
  hasNext,
  onPageChange,
}: SearchPaginationProps) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav className={styles.pagination} aria-label="Pagination">
      <p className={styles.status}>
        Page {page} of {totalPages}
      </p>

      <div className={styles.controls}>
        <Button
          variant="secondary"
          size="small"
          disabled={!hasPrevious}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </Button>

        <Button
          variant="secondary"
          size="small"
          disabled={!hasNext}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </nav>
  );
}