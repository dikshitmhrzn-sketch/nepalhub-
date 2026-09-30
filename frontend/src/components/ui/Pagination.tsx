import { Button } from './Button';

import styles from './Pagination.module.css';

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  label?: string;
}

/**
 * Previous/next paging for any list endpoint. The API clamps out-of-range paging
 * arguments, so this only needs to offer the steps that exist.
 */
export function Pagination({
  page,
  totalPages,
  totalCount,
  onPageChange,
  label = 'results',
}: PaginationProps) {
  if (totalCount === 0) {
    return null;
  }

  return (
    <nav className={styles.pagination} aria-label="Pagination">
      <p className={styles.status}>
        Page {page} of {Math.max(totalPages, 1)} · {totalCount} {label}
      </p>

      <div className={styles.controls}>
        <Button
          variant="secondary"
          size="small"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </Button>

        <Button
          variant="secondary"
          size="small"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </nav>
  );
}
