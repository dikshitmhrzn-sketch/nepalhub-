/** Mirrors the audit-log contracts exposed by the ASP.NET Core API. */

/**
 * One entry of the trail: who did what to which subject, with the before and after as
 * JSON exactly as the API stored them. The strings are shown verbatim on screen — the
 * log is a record, not a form, and reformatting it would risk changing what it says.
 */
export interface AuditLogEntry {
  id: string;
  action: string;
  targetType: string;
  targetId: string;
  adminName: string | null;
  before: string | null;
  after: string | null;
  ip: string | null;
  createdAt: string;
}

/** The one shape every paged endpoint in the API returns. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
