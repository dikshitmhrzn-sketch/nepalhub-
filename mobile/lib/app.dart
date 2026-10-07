import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/api/api_client.dart';
import 'core/auth/session_controller.dart';
import 'core/config/api_environment.dart';
import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/health/api.dart';
import 'package:go_router/go_router.dart';

class NepalHubApp extends StatefulWidget {
  const NepalHubApp({super.key, required this.client});

  final ApiClient client;

  @override
  State<NepalHubApp> createState() => _NepalHubAppState();
}

class _NepalHubAppState extends State<NepalHubApp> {
  late final SessionController _session;
  late final GoRouter _router;

  @override
  void initState() {
    super.initState();
    _session = SessionController(widget.client);
    _router = createAppRouter();
    _session.restore();
  }

  @override
  void dispose() {
    _session.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: widget.client),
        ChangeNotifierProvider<SessionController>.value(value: _session),
        Provider<HealthApi>.value(value: HealthApi(widget.client)),
      ],
      child: MaterialApp.router(
        title: ApiEnvironment.appName,
        debugShowCheckedModeBanner: false,
        theme: NhTheme.light(),
        darkTheme: NhTheme.dark(),
        themeMode: ThemeMode.system,
        routerConfig: _router,
      ),
    );
  }
}
