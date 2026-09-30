import { Navigate, Outlet, useLocation } from 'react-router';

import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';

/**
 * Keeps a signed-in visitor who is not staff away from the management pages. The
 * API enforces the same rule with its `staff-only` policy, so this only saves the
 * visitor from a pointless request.
 */
export function RequireStaff() {
  const { status, isStaff } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return <StateMessage tone="loading" title="Checking your session" />;
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (!isStaff) {
    return (
      <StateMessage
        tone="error"
        title="This page is for moderators and administrators"
        description="Your account does not have permission to manage calculators."
      />
    );
  }

  return <Outlet />;
}
