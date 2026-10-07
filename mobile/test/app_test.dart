import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/app.dart';

import 'helpers/fake_adapter.dart';

void main() {
  testWidgets('the home screen renders against the API', (tester) async {
    final api = TestApi();
    api.onJson(
      'GET',
      '/auth/session',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'authenticated': false, 'user': null},
        'error': null,
      },
    );
    api.onJson(
      'GET',
      '/health',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{
          'status': 'healthy',
          'database': 'connected',
          'timestamp': '2026-10-07T05:30:00.0000000+00:00',
        },
        'error': null,
      },
    );

    await tester.pumpWidget(NepalHubApp(client: api.client));
    await tester.pumpAndSettle();

    expect(find.text('One useful website for everyday Nepal.'), findsOneWidget);
    expect(find.text('API status'), findsOneWidget);
    expect(find.text('healthy'), findsOneWidget);
    expect(find.text('connected'), findsOneWidget);
    expect(find.text('Browse'), findsOneWidget);
    expect(find.text('Search'), findsOneWidget);
    expect(find.text('Account'), findsOneWidget);
  });

  testWidgets('a module tile pushes its list screen inside the shell',
      (tester) async {
    final api = TestApi();
    api.onJson(
      'GET',
      '/auth/session',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'authenticated': false, 'user': null},
        'error': null,
      },
    );
    api.onJson(
      'GET',
      '/health',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{
          'status': 'healthy',
          'database': 'connected',
          'timestamp': '2026-10-07T05:30:00.0000000+00:00',
        },
        'error': null,
      },
    );
    api.onJson(
      'GET',
      '/jobs',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{
          'items': <Object?>[],
          'page': 1,
          'pageSize': 24,
          'totalCount': 0,
          'totalPages': 0,
          'hasPrevious': false,
          'hasNext': false,
        },
        'error': null,
      },
    );

    await tester.pumpWidget(NepalHubApp(client: api.client));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Browse'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Jobs'));
    await tester.pumpAndSettle();

    expect(find.text('No jobs found'), findsOneWidget);
    expect(find.text('Filters'), findsOneWidget);
    expect(find.text('Account'), findsOneWidget);
  });
}
