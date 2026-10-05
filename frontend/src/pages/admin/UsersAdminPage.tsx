import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';

import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { Pagination } from '@/components/ui/Pagination';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import {
  activateUser,
  deactivateUser,
  grantUserRole,
  revokeUserRole,
} from '@/features/useradmin/api';
import { useUsers } from '@/features/useradmin/useUsers';
import {
  assignableRoles,
  roleLabels,
  userStatusFilters,
  userStatusLabels,
  type PlatformRole,
  type UserAdminSummary,
} from '@/features/useradmin/types';
import { getErrorMessage } from '@/lib/apiErrors';

import styles from './UsersAdminPage.module.css';

const PAGE_SIZE = 20;

/**
 * The account list. The URL owns the filters and the page, so "every disabled moderator"
 * can be shared or reloaded like any other view, and a window with no accounts in it says
 * so rather than rendering an empty table.
 */
export function UsersAdminPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { user: me } = useAuth();

  const page = Math.max(Number(searchParams.get('page') ?? '1') || 1, 1);
  const search = searchParams.get('search') ?? '';
  const requestedStatus = searchParams.get('status');
  const status = userStatusFilters.find((value) => value === requestedStatus) ?? 'all';
  const requestedRole = searchParams.get('role');
  const role = assignableRoles.find((value) => value === requestedRole);

  const [searchInput, setSearchInput] = useState(search);
  const list = useUsers({ page, pageSize: PAGE_SIZE, search: search || undefined, status, role });

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

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Accounts</h1>
        <p className={styles.subtitle}>
          Who is on the platform, what they are allowed to do, and whether they can still sign in.
          Changes take effect at the account&rsquo;s next sign-in, and every one of them is written to
          the audit log.
        </p>
      </header>

      <form className={styles.filters} onSubmit={handleSearchSubmit}>
        <label className={styles.searchField}>
          <span className={styles.filterLabel}>Search</span>
          <input
            className={styles.searchInput}
            type="search"
            value={searchInput}
            placeholder="Address or name"
            onChange={(event) => setSearchInput(event.target.value)}
          />
        </label>

        <label className={styles.selectField}>
          <span className={styles.filterLabel}>Status</span>
          <select
            className={styles.select}
            value={status}
            onChange={(event) =>
              updateParams({
                page: null,
                status: event.target.value === 'all' ? null : event.target.value,
              })
            }
          >
            {userStatusFilters.map((value) => (
              <option key={value} value={value}>
                {userStatusLabels[value]}
              </option>
            ))}
          </select>
        </label>

        <label className={styles.selectField}>
          <span className={styles.filterLabel}>Role</span>
          <select
            className={styles.select}
            value={role ?? ''}
            onChange={(event) =>
              updateParams({ page: null, role: event.target.value || null })
            }
          >
            <option value="">All roles</option>
            {assignableRoles.map((value) => (
              <option key={value} value={value}>
                {roleLabels[value]}
              </option>
            ))}
          </select>
        </label>

        <Button type="submit" size="small" variant="secondary">
          Search
        </Button>
      </form>

      {list.status === 'loading' ? <StateMessage tone="loading" title="Loading accounts" /> : null}

      {list.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the accounts"
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
          title="No accounts match"
          description="Try a different search or clear the filters to see everyone."
        />
      ) : null}

      {list.status === 'success' && list.data.items.length > 0 ? (
        <>
          <ul className={styles.list}>
            {list.data.items.map((account) => (
              <UserRow
                key={account.id}
                account={account}
                isSelf={account.id === me?.id}
                onActed={list.reload}
              />
            ))}
          </ul>

          <Pagination
            page={list.data.page}
            totalPages={list.data.totalPages}
            totalCount={list.data.totalCount}
            label="accounts"
            onPageChange={(next) => updateParams({ page: String(next) })}
          />
        </>
      ) : null}
    </Container>
  );
}

interface UserRowProps {
  account: UserAdminSummary;
  isSelf: boolean;
  onActed: () => void;
}

/**
 * One account and everything an administrator may do to it. The administrator's own row
 * has its actions disabled rather than hidden: seeing the row with its roles and an
 * explanation answers "why can I not click this" without a round trip that would be
 * refused anyway.
 */
function UserRow({ account, isSelf, onActed }: UserRowProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [grantRole, setGrantRole] = useState('');

  const availableRoles = assignableRoles.filter((value) => !account.roles.includes(value));

  async function act(run: () => Promise<unknown>) {
    setBusy(true);
    setError(null);

    try {
      await run();
      onActed();
    } catch (actionError) {
      setError(getErrorMessage(actionError, 'That change could not be saved.'));
      setBusy(false);
    }
  }

  return (
    <li className={account.isActive ? styles.row : `${styles.row} ${styles.rowDisabled}`}>
      <div className={styles.identity}>
        <p className={styles.name}>
          {account.fullName}
          {isSelf ? <span className={styles.selfMark}> (you)</span> : null}
        </p>
        <p className={styles.email}>{account.email}</p>
      </div>

      <span
        className={
          account.isActive
            ? `${styles.status} ${styles.statusActive}`
            : `${styles.status} ${styles.statusInactive}`
        }
      >
        {account.isActive ? 'Active' : 'Disabled'}
      </span>

      <ul className={styles.roles}>
        {account.roles.map((role) => {
          const known = assignableRoles.find((value) => value === role);

          return (
            <li key={role} className={styles.roleChip}>
              {known ? roleLabels[known] : role}
              <button
                type="button"
                className={styles.roleRemove}
                aria-label={`Remove ${known ? roleLabels[known] : role} role`}
                title={`Remove the ${known ? roleLabels[known] : role} role`}
                disabled={isSelf || busy}
                onClick={() => void act(() => revokeUserRole(account.id, role as PlatformRole))}
              >
                ×
              </button>
            </li>
          );
        })}
      </ul>

      <div className={styles.actions}>
        <Button
          size="small"
          variant={account.isActive ? 'ghost' : 'secondary'}
          disabled={isSelf || busy}
          onClick={() =>
            void act(() => (account.isActive ? deactivateUser(account.id) : activateUser(account.id)))
          }
        >
          {busy
            ? 'Saving…'
            : account.isActive
              ? 'Disable'
              : 'Enable'}
        </Button>

        {availableRoles.length > 0 ? (
          <span className={styles.grantGroup}>
            <select
              className={styles.grantSelect}
              value={grantRole}
              aria-label="Role to grant"
              disabled={isSelf || busy}
              onChange={(event) => setGrantRole(event.target.value)}
            >
              <option value="">Add role…</option>
              {availableRoles.map((value) => (
                <option key={value} value={value}>
                  {roleLabels[value]}
                </option>
              ))}
            </select>
            <Button
              size="small"
              variant="secondary"
              disabled={isSelf || busy || !grantRole}
              onClick={() => {
                const role = grantRole as PlatformRole;
                setGrantRole('');
                void act(() => grantUserRole(account.id, role));
              }}
            >
              Grant
            </Button>
          </span>
        ) : null}
      </div>

      {isSelf ? (
        <p className={styles.selfNote}>
          Your own account cannot be changed from here, so an administrator cannot lock themselves
          out by accident.
        </p>
      ) : null}

      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : null}
    </li>
  );
}
