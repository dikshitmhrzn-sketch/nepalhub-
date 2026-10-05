/** Mirrors the user-administration contracts exposed by the ASP.NET Core API. */

/** One account as the administrator's list shows it. */
export interface UserAdminSummary {
  id: string;
  email: string;
  fullName: string;
  level: number;
  xp: number;
  isActive: boolean;
  roles: string[];
  createdAt: string;
}

/** The list's status window. `all` is the API's own default. */
export type UserStatusFilter = 'all' | 'active' | 'inactive';

export const userStatusFilters: readonly UserStatusFilter[] = ['all', 'active', 'inactive'];

export const userStatusLabels: Record<UserStatusFilter, string> = {
  all: 'All',
  active: 'Active',
  inactive: 'Disabled',
};

/** The four roles the platform can hand out, with the name the API expects. */
export const assignableRoles = ['user', 'business', 'moderator', 'admin'] as const;

export type PlatformRole = (typeof assignableRoles)[number];

/** What the roles mean on screen; the API only ever sees the wire name. */
export const roleLabels: Record<PlatformRole, string> = {
  user: 'Member',
  business: 'Business',
  moderator: 'Moderator',
  admin: 'Administrator',
};

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
