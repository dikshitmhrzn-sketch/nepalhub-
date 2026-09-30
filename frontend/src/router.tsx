import { createBrowserRouter } from 'react-router';

import { RequireAuth } from '@/components/auth/RequireAuth';
import { RequireStaff } from '@/components/auth/RequireStaff';
import { AppShell } from '@/components/layout/AppShell';
import { AccountPage } from '@/pages/AccountPage';
import { CalculatorAdminPage } from '@/pages/admin/CalculatorAdminPage';
import { QuizAdminPage } from '@/pages/admin/QuizAdminPage';
import { CalculatorPage } from '@/pages/CalculatorPage';
import { HomePage } from '@/pages/HomePage';
import { LoginPage } from '@/pages/LoginPage';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { QuizPage } from '@/pages/QuizPage';
import { RegisterPage } from '@/pages/RegisterPage';
import { ToolsPage } from '@/pages/ToolsPage';

export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'tools', element: <ToolsPage /> },
      { path: 'quiz', element: <QuizPage /> },
      { path: 'calculators/:slug', element: <CalculatorPage /> },
      {
        element: <RequireAuth />,
        children: [{ path: 'account', element: <AccountPage /> }],
      },
      {
        element: <RequireStaff />,
        children: [
          { path: 'admin/calculators', element: <CalculatorAdminPage /> },
          { path: 'admin/quiz', element: <QuizAdminPage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
