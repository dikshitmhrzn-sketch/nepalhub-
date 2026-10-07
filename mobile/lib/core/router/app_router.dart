import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/health/api.dart';
import '../widgets/app_shell.dart';
import '../../pages/account_page.dart';
import '../../pages/browse_page.dart';
import '../../pages/home_page.dart';
import '../../pages/misc_pages.dart';
import '../../pages/search_page.dart';

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
