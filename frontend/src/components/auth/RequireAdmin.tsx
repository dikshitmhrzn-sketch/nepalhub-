import { Navigate, Outlet, useLocation } from 'react-router';

import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';

/**
 * Keeps everyone but administrators away from the account and audit screens. The API
 * enforces the same rule with its `admin-only` policy, so this only saves the visitor
 * from a pointless request — and from a management page that could never load.
 */
export function RequireAdmin() {
  const { status, isAdmin } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return <StateMessage tone="loading" title="Checking your session" />;
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (!isAdmin) {
    return (
      <StateMessage
        tone="error"
        title="This page is for administrators"
        description="Your account does not have permission to administer accounts or read the audit log."
      />
    );
  }

  return <Outlet />;
}
