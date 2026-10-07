import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/features/search/api.dart';

import '../helpers/fake_adapter.dart';

Map<String, Object?> searchEnvelope({
  int totalCount = 2,
  List<Object?> groups = const <Object?>[],
}) =>
    <String, Object?>{
      'success': true,
      'data': <String, Object?>{
        'query': 'kathmandu',
        'page': 1,
        'pageSize': 20,
        'totalCount': totalCount,
        'truncated': false,
        'totalPages': 1,
        'hasPrevious': false,
        'hasNext': false,
        'groups': groups,
      },
      'error': null,
    };

void main() {
  late TestApi api;

  setUp(() {
    api = TestApi();
  });

  test('search sends the query, page and module', () async {
    api.onJson('GET', '/search', searchEnvelope());

    await SearchApi(api.client).search(query: 'kathmandu', page: 3, module: 'Jobs');

    final query = api.requestsWhere('GET', '/search').single.query!;
    expect(query['q'], 'kathmandu');
    expect(query['page'], 3);
    expect(query['module'], 'Jobs');
    expect(query['pageSize'], 20);
  });

  test('groups and items decode, including the client side route', () async {
    api.onJson(
      'GET',
      '/search',
      searchEnvelope(
        groups: <Object?>[
          <String, Object?>{
            'module': 'Jobs',
            'label': 'Jobs',
            'totalCount': 1,
            'items': <Object?>[
              <String, Object?>{
                'module': 'Jobs',
                'referenceId': 'j-1',
                'slug': 'flutter-dev',
                'title': 'Flutter developer',
                'summary': 'Remote role',
                'category': 'InformationTechnology',
                'organisation': 'Himalaya Labs',
                'workMode': 'Remote',
                'isVerified': true,
              },
            ],
          },
          <String, Object?>{
            'module': 'Calculators',
            'label': 'Tools & calculators',
            'totalCount': 1,
            'items': <Object?>[
              <String, Object?>{
                'module': 'Calculators',
                'referenceId': 'c-1',
                'slug': 'loan-emi',
                'title': 'Loan EMI',
                'category': 'Finance',
                'isVerified': false,
              },
            ],
          },
        ],
      ),
    );

    final result = await SearchApi(api.client).search(query: 'kathmandu');

    expect(result.totalCount, 2);
    expect(result.groups, hasLength(2));
    final job = result.groups.first.items.single;
    expect(job.route, '/jobs/flutter-dev');
    expect(job.subtitle, 'InformationTechnology  ·  Himalaya Labs');
    expect(result.groups.last.items.single.route, '/calculators/loan-emi');
    expect(SearchItem.fromJson(<String, Object?>{
      'module': 'Students',
      'slug': 'physics-notes',
      'title': 'Physics notes',
    }).route, '/students/resources/physics-notes');
  });

  test('an empty search is reported as empty', () async {
    api.onJson('GET', '/search', searchEnvelope(totalCount: 0));

    final result = await SearchApi(api.client).search(query: 'zzzz');

    expect(result.isEmpty, isTrue);
    expect(result.groups, isEmpty);
  });
}
