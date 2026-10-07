import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/health/api.dart';
import '../widgets/app_shell.dart';
import '../../pages/account_page.dart';
import '../../pages/browse_page.dart';
import '../../pages/business_page.dart';
import '../../pages/businesses_page.dart';
import '../../pages/calculator_page.dart';
import '../../pages/deal_page.dart';
import '../../pages/deals_page.dart';
import '../../pages/event_page.dart';
import '../../pages/events_page.dart';
import '../../pages/home_page.dart';
import '../../pages/job_page.dart';
import '../../pages/jobs_page.dart';
import '../../pages/leaderboard_page.dart';
import '../../pages/misc_pages.dart';
import '../../pages/quiz_page.dart';
import '../../pages/search_page.dart';
import '../../pages/student_resource_page.dart';
import '../../pages/students_page.dart';
import '../../pages/tools_page.dart';

String _slug(GoRouterState state) => state.pathParameters['slug'] ?? '';

GoRouter createAppRouter() => GoRouter(
      initialLocation: '/',
      errorBuilder: (context, state) => const NotFoundPage(),
      routes: <RouteBase>[
        StatefulShellRoute.indexedStack(
          builder: (context, state, navigationShell) => AppShell(
            navigationShell: navigationShell,
          ),
          branches: <StatefulShellBranch>[
            StatefulShellBranch(
              routes: <RouteBase>[
                GoRoute(
                  path: '/',
                  pageBuilder: (context, state) => NoTransitionPage<void>(
                    key: state.pageKey,
                    child: HomePage(healthApi: context.read<HealthApi>()),
                  ),
                ),
              ],
            ),
            StatefulShellBranch(
              routes: <RouteBase>[
                GoRoute(
                  path: '/browse',
                  pageBuilder: (context, state) => NoTransitionPage<void>(
                    key: state.pageKey,
                    child: const BrowsePage(),
                  ),
                ),
                GoRoute(
                  path: '/tools',
                  builder: (context, state) => const ToolsPage(),
                ),
                GoRoute(
                  path: '/calculators/:slug',
                  builder: (context, state) => CalculatorPage(slug: _slug(state)),
                ),
                GoRoute(
                  path: '/quiz',
                  builder: (context, state) => const QuizPage(),
                ),
                GoRoute(
                  path: '/students',
                  builder: (context, state) => const StudentsPage(),
                ),
                GoRoute(
                  path: '/students/resources/:slug',
                  builder: (context, state) => StudentResourcePage(slug: _slug(state)),
                ),
                GoRoute(
                  path: '/jobs',
                  builder: (context, state) => const JobsPage(),
                ),
                GoRoute(
                  path: '/jobs/:slug',
                  builder: (context, state) => JobPage(slug: _slug(state)),
                ),
                GoRoute(
                  path: '/businesses',
                  builder: (context, state) => const BusinessesPage(),
                ),
                GoRoute(
                  path: '/businesses/:slug',
                  builder: (context, state) => BusinessPage(slug: _slug(state)),
                ),
                GoRoute(
                  path: '/deals',
                  builder: (context, state) => const DealsPage(),
                ),
                GoRoute(
                  path: '/deals/:slug',
                  builder: (context, state) => DealPage(slug: _slug(state)),
                ),
                GoRoute(
                  path: '/events',
                  builder: (context, state) => const EventsPage(),
                ),
                GoRoute(
                  path: '/events/:slug',
                  builder: (context, state) => EventPage(slug: _slug(state)),
                ),
                GoRoute(
                  path: '/leaderboard',
                  builder: (context, state) => const LeaderboardPage(),
                ),
              ],
            ),
            StatefulShellBranch(
              routes: <RouteBase>[
                GoRoute(
                  path: '/search',
                  pageBuilder: (context, state) => NoTransitionPage<void>(
                    key: state.pageKey,
                    child: const SearchPage(),
                  ),
                ),
              ],
            ),
            StatefulShellBranch(
              routes: <RouteBase>[
                GoRoute(
                  path: '/account',
                  pageBuilder: (context, state) => NoTransitionPage<void>(
                    key: state.pageKey,
                    child: const AccountPage(),
                  ),
                ),
                GoRoute(
                  path: '/settings',
                  builder: (context, state) => const SettingsPage(),
                ),
              ],
            ),
          ],
        ),
      ],
    );
