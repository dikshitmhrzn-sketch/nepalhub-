import { createBrowserRouter } from 'react-router';

import { RequireAuth } from '@/components/auth/RequireAuth';
import { AppShell } from '@/components/layout/AppShell';
import { AccountPage } from '@/pages/AccountPage';
import { HomePage } from '@/pages/HomePage';
import { LoginPage } from '@/pages/LoginPage';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { RegisterPage } from '@/pages/RegisterPage';

export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      {
        element: <RequireAuth />,
        children: [{ path: 'account', element: <AccountPage /> }],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
