import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/core/auth/session_controller.dart';
import 'package:nepal_hub/pages/account_page.dart';
import 'package:nepal_hub/pages/login_page.dart';
import 'package:nepal_hub/pages/register_page.dart';
import 'package:provider/provider.dart';

import '../helpers/fake_adapter.dart';

const Map<String, Object?> sessionUser = <String, Object?>{
  'id': 'u-1',
  'email': 'asha@example.com',
  'fullName': 'Asha Kumari',
  'avatarUrl': null,
  'isEmailConfirmed': true,
  'level': 3,
  'xp': 120,
  'roles': ['user'],
};

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

GoRouter accountRouter() => GoRouter(
      initialLocation: '/account',
      routes: <RouteBase>[
        GoRoute(
          path: '/account',
          builder: (context, state) => const AccountPage(),
        ),
        GoRoute(
          path: '/login',
          builder: (context, state) => const LoginPage(),
        ),
        GoRoute(
          path: '/register',
          builder: (context, state) => const RegisterPage(),
        ),
      ],
    );

Future<void> pumpAccount(
  WidgetTester tester,
  TestApi api,
  SessionController session,
) async {
  await tester.pumpWidget(
    ChangeNotifierProvider<SessionController>.value(
      value: session,
      child: Provider<ApiClient>.value(
        value: api.client,
        child: MaterialApp.router(routerConfig: accountRouter()),
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
    api.onJson('GET', '/plans', envelope(<Object?>[]));
    api.onJson('GET', '/auth/session', envelope(<String, Object?>{
      'authenticated': false,
      'user': null,
    }), times: 3);
    api.onJson('GET', '/auth/csrf', envelope(<String, Object?>{
      'headerName': 'X-CSRF-TOKEN',
      'token': 'csrf-token-1',
    }), times: 3);
    session = SessionController(api.client);
  });

  testWidgets('an anonymous account offers sign in and registration',
      (tester) async {
    await tester.runAsync(() => session.restore());
    await pumpAccount(tester, api, session);

    expect(find.text('You are not signed in'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Create an account'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Welcome back'), findsOneWidget);
  });

  testWidgets('a signed in account shows the profile and can sign out',
      (tester) async {
    api.onJson('POST', '/auth/login', envelope(sessionUser));
    api.onJson('POST', '/auth/logout', envelope(<String, Object?>{
      'signedOut': true,
    }));

    final signedIn =
        await tester.runAsync(() => session.signIn(email: 'asha@example.com', password: 'Str0ng!pass'));
    expect(signedIn!.user, isNotNull);

    await pumpAccount(tester, api, session);

    expect(find.text('Asha Kumari'), findsOneWidget);
    expect(find.text('asha@example.com'), findsOneWidget);
    expect(find.text('Level 3  ·  120 XP'), findsOneWidget);

    await tester.tap(find.widgetWithText(OutlinedButton, 'Sign out'));
    await tester.pumpAndSettle();

    expect(api.countOf('POST', '/auth/logout'), 1);
    expect(find.text('You are not signed in'), findsOneWidget);
  });
}
