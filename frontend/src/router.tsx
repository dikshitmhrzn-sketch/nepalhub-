import { createBrowserRouter } from 'react-router';

import { RequireAuth } from '@/components/auth/RequireAuth';
import { RequireStaff } from '@/components/auth/RequireStaff';
import { AppShell } from '@/components/layout/AppShell';
import { AccountPage } from '@/pages/AccountPage';
import { BusinessesAdminPage } from '@/pages/admin/BusinessesAdminPage';
import { CalculatorAdminPage } from '@/pages/admin/CalculatorAdminPage';
import { DealsAdminPage } from '@/pages/admin/DealsAdminPage';
import { EventsAdminPage } from '@/pages/admin/EventsAdminPage';
import { JobsAdminPage } from '@/pages/admin/JobsAdminPage';
import { QuizAdminPage } from '@/pages/admin/QuizAdminPage';
import { ResourceAdminPage } from '@/pages/admin/ResourceAdminPage';
import { BusinessPage } from '@/pages/BusinessPage';
import { BusinessesPage } from '@/pages/BusinessesPage';
import { CalculatorPage } from '@/pages/CalculatorPage';
import { DealPage } from '@/pages/DealPage';
import { DealsPage } from '@/pages/DealsPage';
import { EventPage } from '@/pages/EventPage';
import { EventsPage } from '@/pages/EventsPage';
import { HomePage } from '@/pages/HomePage';
import { JobPage } from '@/pages/JobPage';
import { JobsPage } from '@/pages/JobsPage';
import { LoginPage } from '@/pages/LoginPage';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { QuizPage } from '@/pages/QuizPage';
import { RegisterPage } from '@/pages/RegisterPage';
import { SearchPage } from '@/pages/SearchPage';
import { StudentResourcePage } from '@/pages/StudentResourcePage';
import { StudentsPage } from '@/pages/StudentsPage';
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
      { path: 'students', element: <StudentsPage /> },
      { path: 'students/resources/:slug', element: <StudentResourcePage /> },
      { path: 'jobs', element: <JobsPage /> },
      { path: 'jobs/:slug', element: <JobPage /> },
      { path: 'businesses', element: <BusinessesPage /> },
      { path: 'businesses/:slug', element: <BusinessPage /> },
      { path: 'deals', element: <DealsPage /> },
      { path: 'deals/:slug', element: <DealPage /> },
      { path: 'events', element: <EventsPage /> },
      { path: 'events/:slug', element: <EventPage /> },
      { path: 'calculators/:slug', element: <CalculatorPage /> },
      { path: 'search', element: <SearchPage /> },
      {
        element: <RequireAuth />,
        children: [{ path: 'account', element: <AccountPage /> }],
      },
      {
        element: <RequireStaff />,
        children: [
          { path: 'admin/calculators', element: <CalculatorAdminPage /> },
          { path: 'admin/quiz', element: <QuizAdminPage /> },
          { path: 'admin/students', element: <ResourceAdminPage /> },
          { path: 'admin/jobs', element: <JobsAdminPage /> },
          { path: 'admin/businesses', element: <BusinessesAdminPage /> },
          { path: 'admin/deals', element: <DealsAdminPage /> },
          { path: 'admin/events', element: <EventsAdminPage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
