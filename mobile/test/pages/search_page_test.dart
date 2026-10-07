import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/pages/search_page.dart';
import 'package:provider/provider.dart';

import '../helpers/fake_adapter.dart';

Map<String, Object?> searchEnvelope() => <String, Object?>{
      'success': true,
      'data': <String, Object?>{
        'query': 'kathmandu',
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'truncated': false,
        'totalPages': 1,
        'hasPrevious': false,
        'hasNext': false,
        'groups': [
          <String, Object?>{
            'module': 'Jobs',
            'label': 'Jobs',
            'totalCount': 1,
            'items': <Object?>[
              <String, Object?>{
                'module': 'Jobs',
                'slug': 'flutter-dev',
                'title': 'Flutter developer',
                'category': 'InformationTechnology',
                'organisation': 'Himalaya Labs',
                'isVerified': true,
              },
            ],
          },
        ],
      },
      'error': null,
    };

Future<void> pumpSearch(WidgetTester tester, TestApi api) async {
  await tester.pumpWidget(
    Provider<ApiClient>.value(
      value: api.client,
      child: const MaterialApp(home: SearchPage()),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('the search page asks for at least two characters',
      (tester) async {
    final api = TestApi();

    await pumpSearch(tester, api);

    expect(find.text('Search everything in one place'), findsOneWidget);
    expect(find.text('Everything'), findsOneWidget);
  });

  testWidgets('typing a query returns grouped results', (tester) async {
    final api = TestApi();
    api.onJson('GET', '/search', searchEnvelope(), times: 3);

    await pumpSearch(tester, api);

    await tester.enterText(find.byType(TextField), 'kathmandu');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();

    expect(api.requestsWhere('GET', '/search').single.query?['q'], 'kathmandu');
    expect(find.text('Flutter developer'), findsOneWidget);
    expect(find.text('InformationTechnology  ·  Himalaya Labs'), findsOneWidget);
  });

  testWidgets('switching the module chip searches again', (tester) async {
    final api = TestApi();
    api.onJson('GET', '/search', searchEnvelope(), times: 3);

    await pumpSearch(tester, api);

    await tester.enterText(find.byType(TextField), 'kathmandu');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Students'));
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();

    final queries = api.requestsWhere('GET', '/search');
    expect(queries, hasLength(2));
    expect(queries.last.query?['module'], 'Students');
  });
}
