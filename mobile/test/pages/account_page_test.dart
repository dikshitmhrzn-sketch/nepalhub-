import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/core/auth/session_controller.dart';
import 'package:nepal_hub/pages/account_page.dart';
import 'package:nepal_hub/pages/login_page.dart';
import 'package:nepal_hub/pages/payments_page.dart';
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
        GoRoute(
          path: '/account/payments',
          builder: (context, state) => const PaymentsPage(),
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

  testWidgets('a signed in account shows badges and the payment history',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    api.onJson('POST', '/auth/login', envelope(sessionUser));
    api.onJson('POST', '/auth/logout', envelope(<String, Object?>{
      'signedOut': true,
    }));
    api.onJson('GET', '/account/badges', envelope(<String, Object?>{
      'awardedCount': 1,
      'badges': <Object?>[
        <String, Object?>{
          'code': 'seven-day-streak',
          'name': '7-Day Streak',
          'description': 'Finish a quiz on seven days in a row.',
          'icon': 'flame',
          'awarded': true,
          'awardedAt': '2026-09-20T09:00:00',
          'current': 7,
          'threshold': 7,
        },
        <String, Object?>{
          'code': 'quiz-enthusiast',
          'name': 'Quiz Enthusiast',
          'description': 'Finish ten daily quizzes.',
          'icon': 'book',
          'awarded': false,
          'awardedAt': null,
          'current': 4,
          'threshold': 10,
        },
      ],
    }));
    api.onJson(
      'GET',
      '/account/payments',
      envelope(<String, Object?>{
        'items': <Object?>[
          <String, Object?>{
            'reference': 'PAY-1001',
            'kind': 'EventTicket',
            'status': 'Succeeded',
            'amountNpr': 500,
            'description': 'Tech meetup ticket',
            'failureCode': null,
            'createdAt': '2026-10-01T09:00:00',
            'settledAt': '2026-10-01T09:02:00',
            'refundedAt': null,
          },
        ],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
        'hasPrevious': false,
        'hasNext': false,
      }),
    );

    await tester.runAsync(
      () => session.signIn(email: 'asha@example.com', password: 'Str0ng!pass'),
    );
    await pumpAccount(tester, api, session);

    expect(find.text('Badges'), findsOneWidget);
    expect(find.text('1 of 2 earned'), findsOneWidget);
    expect(find.text('7-Day Streak'), findsOneWidget);
    expect(find.text('Earned 20 Sep 2026'), findsOneWidget);
    expect(find.text('Quiz Enthusiast'), findsOneWidget);
    expect(find.text('4 of 10'), findsOneWidget);
    expect(api.countOf('GET', '/account/badges'), 1);

    await tester.tap(find.widgetWithText(OutlinedButton, 'Your payments'));
    await tester.pumpAndSettle();

    expect(find.byType(PaymentsPage), findsOneWidget);
    expect(find.text('Tech meetup ticket'), findsOneWidget);
    expect(find.text('Rs 500'), findsOneWidget);
  });
}
