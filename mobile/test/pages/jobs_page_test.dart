import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/pages/jobs_page.dart';
import 'package:provider/provider.dart';

import '../helpers/fake_adapter.dart';

Map<String, Object?> jobPage(String category) => <String, Object?>{
      'success': true,
      'data': <String, Object?>{
        'items': [
          <String, Object?>{
            'slug': 'job-1',
            'title': 'Flutter developer',
            'organization': 'Himalaya Labs',
            'category': category,
            'employmentType': 'FullTime',
            'workMode': 'Remote',
            'districtName': 'Kathmandu',
            'provinceName': 'Bagmati',
            'applicationDeadline': '2026-11-01',
            'status': 'Open',
            'createdAt': '2026-10-01T04:00:00Z',
          },
        ],
        'page': 1,
        'pageSize': 24,
        'totalCount': 1,
        'totalPages': 1,
        'hasPrevious': false,
        'hasNext': false,
      },
      'error': null,
    };

Future<void> pumpJobs(WidgetTester tester, TestApi api) async {
  await tester.pumpWidget(
    Provider<ApiClient>.value(
      value: api.client,
      child: const MaterialApp(home: JobsPage()),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('the jobs list renders results from the API', (tester) async {
    final api = TestApi();
    api.onJson('GET', '/jobs', jobPage('InformationTechnology'));

    await pumpJobs(tester, api);

    expect(find.text('Flutter developer'), findsOneWidget);
    expect(find.text('Himalaya Labs'), findsOneWidget);
    expect(find.text('Full Time'), findsOneWidget);
  });

  testWidgets('applying a filter reloads the list with the new query',
      (tester) async {
    final api = TestApi();
    api.onJson('GET', '/jobs', jobPage('InformationTechnology'), times: 5);

    await pumpJobs(tester, api);
    expect(api.requestsWhere('GET', '/jobs').single.query?['category'], isNull);

    await tester.tap(find.text('Filters'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Teaching'));
    await tester.tap(find.text('Apply'));
    await tester.pumpAndSettle();

    expect(api.requestsWhere('GET', '/jobs'), hasLength(2));
    expect(api.requestsWhere('GET', '/jobs').last.query?['category'], 'Teaching');
  });

  testWidgets('an api failure shows a retry message', (tester) async {
    final api = TestApi();
    api.onError('GET', '/jobs', 400, 'category_invalid', 'Choose a valid category.');

    await pumpJobs(tester, api);

    expect(find.text('Something went wrong'), findsOneWidget);
    expect(find.text('Choose a valid category.'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
  });

  testWidgets('picking a province and district filters the list', (tester) async {
    final api = TestApi();
    api.onJson(
      'GET',
      '/reference/provinces',
      <Object?>[
        <String, Object?>{'id': 'p1', 'code': 'bagmati', 'name': 'Bagmati'},
        <String, Object?>{'id': 'p2', 'code': 'koshi', 'name': 'Koshi'},
      ],
      times: 3,
    );
    api.onJson(
      'GET',
      '/reference/provinces/bagmati/districts',
      <Object?>[
        <String, Object?>{
          'id': 'd1',
          'code': 'bagmati-kathmandu',
          'name': 'Kathmandu',
        },
      ],
      times: 3,
    );
    api.onJson('GET', '/jobs', jobPage('InformationTechnology'), times: 5);

    await pumpJobs(tester, api);

    expect(find.text('Bagmati'), findsOneWidget);

    await tester.tap(find.text('Bagmati'));
    await tester.pumpAndSettle();

    expect(api.requestsWhere('GET', '/jobs').last.query?['province'], 'bagmati');
    expect(find.text('Kathmandu'), findsOneWidget);

    await tester.tap(find.text('Kathmandu'));
    await tester.pumpAndSettle();

    final last = api.requestsWhere('GET', '/jobs').last.query!;
    expect(last['province'], 'bagmati');
    expect(last['district'], 'bagmati-kathmandu');
  });
}
