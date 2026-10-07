import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/core/auth/session_controller.dart';
import 'package:nepal_hub/pages/event_page.dart';
import 'package:nepal_hub/pages/login_page.dart';
import 'package:provider/provider.dart';

import '../helpers/fake_adapter.dart';

const Map<String, Object?> eventPayload = <String, Object?>{
  'id': 'e-1',
  'slug': 'tech-meetup',
  'title': 'Tech meetup',
  'summary': 'An evening of talks',
  'description': 'Talks, demos and networking for builders in Kathmandu.',
  'category': 'Technology',
  'organiser': 'Himalaya Labs',
  'businessName': 'Himalaya Labs',
  'districtName': 'Kathmandu',
  'provinceName': 'Bagmati',
  'venue': 'Thapathali',
  'startsAt': '2026-11-05T10:00:00',
  'endsAt': '2026-11-05T17:00:00',
  'isOngoing': false,
  'businessSlug': 'himalaya-labs',
  'registrationUrl': null,
  'capacity': 40,
  'registrationCount': 12,
  'remainingSpots': 28,
  'ticketPrice': 0,
};

const Map<String, Object?> bookingPayload = <String, Object?>{
  'id': 'b-1',
  'eventId': 'e-1',
  'eventSlug': 'tech-meetup',
  'title': 'Tech meetup',
  'startsAt': '2026-11-05T10:00:00',
  'quantity': 1,
  'status': 'Booked',
  'createdAt': '2026-10-01T09:00:00',
  'cancelledAt': null,
};

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

GoRouter eventRouter() => GoRouter(
      initialLocation: '/events/tech-meetup',
      routes: <RouteBase>[
        GoRoute(
          path: '/events/tech-meetup',
          builder: (context, state) => const EventPage(slug: 'tech-meetup'),
        ),
        GoRoute(
          path: '/login',
          builder: (context, state) => const LoginPage(),
        ),
      ],
    );

void registerSessionRoutes(TestApi api) {
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
}

Future<void> pumpEvent(
  WidgetTester tester,
  TestApi api,
  SessionController session,
) async {
  await tester.pumpWidget(
    ChangeNotifierProvider<SessionController>.value(
      value: session,
      child: Provider<ApiClient>.value(
        value: api.client,
        child: MaterialApp.router(routerConfig: eventRouter()),
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
    registerSessionRoutes(api);
    api.onJson('GET', '/events/tech-meetup', envelope(eventPayload));
    session = SessionController(api.client);
  });

  testWidgets('a visitor is taken to sign in before booking', (tester) async {
    await tester.runAsync(() => session.restore());
    await pumpEvent(tester, api, session);

    expect(find.text('Book your place'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Book my place'));
    await tester.pumpAndSettle();

    expect(find.text('Welcome back'), findsOneWidget);
    expect(api.countOf('POST', '/events/tech-meetup/booking'), 0);
  });

  testWidgets('a signed in member books a place', (tester) async {
    await tester.runAsync(() => session.restore());
    await tester.runAsync(
      () => session.signIn(email: 'asha@example.com', password: 'Str0ng!pass'),
    );
    api.onJson('POST', '/events/tech-meetup/booking', envelope(bookingPayload));
    await pumpEvent(tester, api, session);

    await tester.tap(find.widgetWithText(FilledButton, 'Book my place'));
    await tester.pumpAndSettle();

    final request = api.requestsWhere('POST', '/events/tech-meetup/booking').single;
    expect(request.jsonBody, <String, Object?>{'quantity': 1});
    expect(request.header('X-CSRF-TOKEN'), 'csrf-token-1');
    expect(find.text('You hold 1 place at this event'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Release my place'), findsOneWidget);
    expect(find.text('Book your place'), findsNothing);
  });

  testWidgets('the place stepper is sent with the booking', (tester) async {
    await tester.runAsync(() => session.restore());
    await tester.runAsync(
      () => session.signIn(email: 'asha@example.com', password: 'Str0ng!pass'),
    );
    api.onJson(
      'POST',
      '/events/tech-meetup/booking',
      envelope(<String, Object?>{
        'id': 'b-2',
        'eventId': 'e-1',
        'eventSlug': 'tech-meetup',
        'title': 'Tech meetup',
        'startsAt': '2026-11-05T10:00:00',
        'quantity': 3,
        'status': 'Booked',
        'createdAt': '2026-10-01T09:00:00',
        'cancelledAt': null,
      }),
    );
    await pumpEvent(tester, api, session);

    await tester.tap(find.byTooltip('One place more'));
    await tester.tap(find.byTooltip('One place more'));
    await tester.pump();
    expect(find.text('3 places'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Book my place'));
    await tester.pumpAndSettle();

    final request = api.requestsWhere('POST', '/events/tech-meetup/booking').single;
    expect(request.jsonBody, <String, Object?>{'quantity': 3});
    expect(find.text('You hold 3 places at this event'), findsOneWidget);
  });

  testWidgets('a full event explains itself instead of failing silently',
      (tester) async {
    await tester.runAsync(() => session.restore());
    await tester.runAsync(
      () => session.signIn(email: 'asha@example.com', password: 'Str0ng!pass'),
    );
    api.onError(
      'POST',
      '/events/tech-meetup/booking',
      409,
      'event_full',
      'This event is fully booked.',
    );
    await pumpEvent(tester, api, session);

    await tester.tap(find.widgetWithText(FilledButton, 'Book my place'));
    await tester.pumpAndSettle();

    expect(find.text('This event is fully booked.'), findsOneWidget);
    expect(find.text('Book your place'), findsOneWidget);
  });

  testWidgets('a booked place can be released again', (tester) async {
    await tester.runAsync(() => session.restore());
    await tester.runAsync(
      () => session.signIn(email: 'asha@example.com', password: 'Str0ng!pass'),
    );
    api.onJson('POST', '/events/tech-meetup/booking', envelope(bookingPayload));
    api.onJson(
      'DELETE',
      '/events/tech-meetup/booking',
      envelope(<String, Object?>{
        'id': 'b-1',
        'eventId': 'e-1',
        'eventSlug': 'tech-meetup',
        'title': 'Tech meetup',
        'startsAt': '2026-11-05T10:00:00',
        'quantity': 1,
        'status': 'Cancelled',
        'createdAt': '2026-10-01T09:00:00',
        'cancelledAt': '2026-10-02T09:00:00',
      }),
    );
    await pumpEvent(tester, api, session);

    await tester.tap(find.widgetWithText(FilledButton, 'Book my place'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(OutlinedButton, 'Release my place'));
    await tester.pumpAndSettle();

    expect(api.countOf('DELETE', '/events/tech-meetup/booking'), 1);
    expect(find.text('Your place has been released.'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Book my place'), findsOneWidget);
  });
}
