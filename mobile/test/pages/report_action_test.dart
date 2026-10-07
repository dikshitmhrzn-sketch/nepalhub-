import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/core/auth/session_controller.dart';
import 'package:nepal_hub/core/widgets/report_action.dart';
import 'package:nepal_hub/pages/login_page.dart';
import 'package:provider/provider.dart';

import '../helpers/fake_adapter.dart';

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

GoRouter reportRouter() => GoRouter(
      initialLocation: '/report',
      routes: <RouteBase>[
        GoRoute(
          path: '/report',
          builder: (context, state) => Scaffold(
            body: ListView(
              padding: EdgeInsets.all(16),
              children: const [
                SizedBox(height: 800),
                ReportAction(
                  targetType: 'Job',
                  targetId: '0f4a1c2e-1111-2222-3333-444455556666',
                ),
              ],
            ),
          ),
        ),
        GoRoute(
          path: '/login',
          builder: (context, state) => const LoginPage(),
        ),
      ],
    );

Future<void> pumpReport(
  WidgetTester tester,
  TestApi api,
  SessionController session,
) async {
  await tester.pumpWidget(
    ChangeNotifierProvider<SessionController>.value(
      value: session,
      child: Provider<ApiClient>.value(
        value: api.client,
        child: MaterialApp.router(routerConfig: reportRouter()),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  late TestApi api;
  late SessionController session;

  setUp(() {
    api = TestApi();
    api.onJson(
      'GET',
      '/auth/session',
      envelope(<String, Object?>{'authenticated': false, 'user': null}),
      times: 3,
    );
    api.onJson(
      'GET',
      '/auth/csrf',
      envelope(<String, Object?>{
        'headerName': 'X-CSRF-TOKEN',
        'token': 'csrf-token-1',
      }),
      times: 5,
    );
    api.onJson(
      'POST',
      '/auth/login',
      envelope(<String, Object?>{
        'id': 'u-1',
        'email': 'asha@example.com',
        'fullName': 'Asha Kumari',
        'avatarUrl': null,
        'isEmailConfirmed': true,
        'level': 3,
        'xp': 120,
        'roles': ['user'],
      }),
      times: 3,
    );
    session = SessionController(api.client);
  });

  testWidgets('a visitor must sign in before reporting', (tester) async {
    await tester.runAsync(() => session.restore());
    await pumpReport(tester, api, session);

    await tester.scrollUntilVisible(
      find.widgetWithText(OutlinedButton, 'Report this listing'),
      300,
    );
    await tester.tap(find.widgetWithText(OutlinedButton, 'Report this listing'));
    await tester.pumpAndSettle();

    expect(find.text('Welcome back'), findsOneWidget);
    expect(api.countOf('POST', '/reports'), 0);
  });

  testWidgets('a signed in member files a report', (tester) async {
    await tester.runAsync(() => session.restore());
    await tester.runAsync(
      () => session.signIn(email: 'asha@example.com', password: 'Str0ng!pass'),
    );
    api.onJson(
      'POST',
      '/reports',
      envelope(<String, Object?>{
        'id': 'r-1',
        'targetType': 'Job',
        'targetId': '0f4a1c2e-1111-2222-3333-444455556666',
        'reason': 'Misleading',
        'status': 'Open',
        'createdAt': '2026-10-01T09:00:00',
      }),
      status: 201,
    );
    await pumpReport(tester, api, session);

    await tester.scrollUntilVisible(
      find.widgetWithText(OutlinedButton, 'Report this listing'),
      300,
    );
    await tester.tap(find.widgetWithText(OutlinedButton, 'Report this listing'));
    await tester.pumpAndSettle();

    expect(find.text('Report Job'), findsOneWidget);

    await tester.tap(find.text('Misleading or false'));
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Send report'));
    await tester.pumpAndSettle();

    final request = api.requestsWhere('POST', '/reports').single;
    expect(request.jsonBody, <String, Object?>{
      'targetType': 'Job',
      'targetId': '0f4a1c2e-1111-2222-3333-444455556666',
      'reason': 'Misleading',
    });
    expect(request.header('X-CSRF-TOKEN'), 'csrf-token-1');
    expect(
      find.text('Report submitted. Our team will review this listing.'),
      findsOneWidget,
    );
  });
}
