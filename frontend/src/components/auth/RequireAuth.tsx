import { Navigate, Outlet, useLocation } from 'react-router';

import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';

/** Keeps a signed-out visitor away from pages that need an account. */
export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return <StateMessage tone="loading" title="Checking your session" />;
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  return <Outlet />;
}
