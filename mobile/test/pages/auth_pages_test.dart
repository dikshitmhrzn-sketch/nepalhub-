import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/core/auth/session_controller.dart';
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

GoRouter authRouter(String initialLocation) => GoRouter(
      initialLocation: initialLocation,
      routes: <RouteBase>[
        GoRoute(
          path: '/login',
          builder: (context, state) => const LoginPage(),
        ),
        GoRoute(
          path: '/register',
          builder: (context, state) => const RegisterPage(),
        ),
        GoRoute(
          path: '/account',
          builder: (context, state) =>
              const Scaffold(body: Center(child: Text('account-screen'))),
        ),
      ],
    );

Future<void> pumpAuth(
  WidgetTester tester,
  TestApi api, {
  String initialLocation = '/login',
}) async {
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: api.client),
        ChangeNotifierProvider<SessionController>(
          create: (_) => SessionController(api.client),
        ),
      ],
      child: MaterialApp.router(routerConfig: authRouter(initialLocation)),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  late TestApi api;

  setUp(() {
    api = TestApi();
    api.onJson('GET', '/auth/csrf', envelope(<String, Object?>{
      'headerName': 'X-CSRF-TOKEN',
      'token': 'csrf-token-1',
    }), times: 3);
  });

  testWidgets('a valid sign in posts the credentials and starts a session',
      (tester) async {
    api.onJson('POST', '/auth/login', envelope(sessionUser));

    await pumpAuth(tester, api);

    await tester.enterText(find.byType(TextFormField).at(0), '  asha@example.com ');
    await tester.enterText(find.byType(TextFormField).at(1), 'Str0ng!pass');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('account-screen'), findsOneWidget);
    expect(api.countOf('GET', '/auth/csrf'), 1);

    final login = api.requestsWhere('POST', '/auth/login').single;
    expect(login.jsonBody, <String, Object?>{
      'email': 'asha@example.com',
      'password': 'Str0ng!pass',
      'rememberMe': true,
    });
    expect(login.header('X-CSRF-TOKEN'), 'csrf-token-1');
  });

  testWidgets('an invalid email keeps the form on screen', (tester) async {
    await pumpAuth(tester, api);

    await tester.enterText(find.byType(TextFormField).at(0), 'not-an-email');
    await tester.enterText(find.byType(TextFormField).at(1), 'Str0ng!pass');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a valid email address.'), findsOneWidget);
    expect(api.countOf('POST', '/auth/login'), 0);
    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
  });

  testWidgets('rejected credentials are shown on the form', (tester) async {
    api.onError(
      'POST',
      '/auth/login',
      401,
      'invalid_credentials',
      'Email or password is incorrect.',
    );

    await pumpAuth(tester, api);

    await tester.enterText(find.byType(TextFormField).at(0), 'asha@example.com');
    await tester.enterText(find.byType(TextFormField).at(1), 'Str0ng!pass');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Email or password is incorrect.'), findsOneWidget);
    expect(find.text('account-screen'), findsNothing);
  });

  testWidgets('the sign in page links to registration', (tester) async {
    await pumpAuth(tester, api);

    await tester.tap(find.widgetWithText(TextButton, 'Create an account'));
    await tester.pumpAndSettle();

    expect(find.text('Join Nepal Hub'), findsOneWidget);
  });

  testWidgets('registration posts the profile and signs the user in',
      (tester) async {
    api.onJson('POST', '/auth/register', envelope(sessionUser), times: 3);

    await pumpAuth(tester, api, initialLocation: '/register');

    await tester.enterText(find.byType(TextFormField).at(0), 'Asha Kumari');
    await tester.enterText(find.byType(TextFormField).at(1), 'asha@example.com');
    await tester.enterText(find.byType(TextFormField).at(2), '9841234567');
    await tester.enterText(find.byType(TextFormField).at(3), 'Str0ng!pass');
    await tester.enterText(find.byType(TextFormField).at(4), 'Str0ng!pass');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('account-screen'), findsOneWidget);
    final register = api.requestsWhere('POST', '/auth/register').single;
    expect(register.jsonBody, <String, Object?>{
      'fullName': 'Asha Kumari',
      'email': 'asha@example.com',
      'password': 'Str0ng!pass',
      'phoneNumber': '9841234567',
    });
    expect(register.header('X-CSRF-TOKEN'), 'csrf-token-1');
  });

  testWidgets('a mismatched confirmation is caught before the request',
      (tester) async {
    await pumpAuth(tester, api, initialLocation: '/register');

    await tester.enterText(find.byType(TextFormField).at(0), 'Asha Kumari');
    await tester.enterText(find.byType(TextFormField).at(1), 'asha@example.com');
    await tester.enterText(find.byType(TextFormField).at(3), 'Str0ng!pass');
    await tester.enterText(find.byType(TextFormField).at(4), 'Different1!');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(find.text('Passwords do not match.'), findsOneWidget);
    expect(api.countOf('POST', '/auth/register'), 0);
  });

  testWidgets('a weak password is explained on the field', (tester) async {
    await pumpAuth(tester, api, initialLocation: '/register');

    await tester.enterText(find.byType(TextFormField).at(3), 'short');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();

    expect(
      find.text(
        'Use at least 8 characters with upper case, lower case, a digit and a symbol.',
      ),
      findsOneWidget,
    );
    expect(api.countOf('POST', '/auth/register'), 0);
  });
}
