import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_exception.dart';
import 'package:nepal_hub/core/offline/reference_cache.dart';
import 'package:nepal_hub/features/calculators/api.dart';
import 'package:nepal_hub/features/plans/api.dart';
import 'package:nepal_hub/features/reference/api.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../helpers/fake_adapter.dart';

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

const List<Object?> provinceRows = <Object?>[
  <String, Object?>{'id': 'p-1', 'code': 'bagmati', 'name': 'Bagmati'},
  <String, Object?>{'id': 'p-2', 'code': 'koshi', 'name': 'Koshi'},
];

const List<Object?> districtRows = <Object?>[
  <String, Object?>{'id': 'd-1', 'code': 'kathmandu', 'name': 'Kathmandu'},
];

const List<Object?> planRows = <Object?>[
  <String, Object?>{
    'id': 'plan-1',
    'tier': 'Featured',
    'name': 'Featured',
    'description': 'A pinned listing.',
    'periodDays': 30,
    'priceNpr': 999,
  },
];

const Map<String, Object?> calculatorPage = <String, Object?>{
  'items': <Object?>[
    <String, Object?>{
      'slug': 'emiflat',
      'name': 'EMI Flat',
      'summary': 'Straight monthly instalment.',
      'category': 'Finance',
    },
  ],
  'page': 1,
  'pageSize': 24,
  'totalCount': 1,
  'totalPages': 1,
  'hasPrevious': false,
  'hasNext': false,
};

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late TestApi api;
  const cache = ReferenceCache();

  setUp(() async {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    await ReferenceCache.open();
    api = TestApi();
  });

  test('a successful load is stored for the next time there is no network',
      () async {
    api.onJson('GET', '/reference/provinces', envelope(provinceRows));

    final fresh = await ReferenceApi(api.client).provinces();

    expect(fresh.map((place) => place.code), <String>['bagmati', 'koshi']);
    expect(await cache.read('provinces'), provinceRows);
  });

  test('an offline request is answered from the stored copy', () async {
    api.onJson('GET', '/reference/provinces', envelope(provinceRows), times: 1);
    api.onError('GET', '/reference/provinces', 500, 'server_error', 'Down.', times: 1);

    final first = await ReferenceApi(api.client).provinces();
    final second = await ReferenceApi(api.client).provinces();

    expect(first, hasLength(2));
    expect(second.map((place) => place.name), <String>['Bagmati', 'Koshi']);
    expect(api.countOf('GET', '/reference/provinces'), 2);
  });

  test('a failure with nothing stored still surfaces the error', () async {
    api.onError('GET', '/reference/provinces', 500, 'server_error', 'Down.');

    await expectLater(
      ReferenceApi(api.client).provinces(),
      throwsA(isA<ApiException>()),
    );
    expect(await cache.read('provinces'), isNull);
  });

  test('districts are stored per province', () async {
    api.onJson(
      'GET',
      '/reference/provinces/bagmati/districts',
      envelope(districtRows),
      times: 1,
    );
    api.onError(
      'GET',
      '/reference/provinces/bagmati/districts',
      500,
      'server_error',
      'Down.',
      times: 1,
    );
    api.onError(
      'GET',
      '/reference/provinces/koshi/districts',
      500,
      'server_error',
      'Down.',
    );

    await ReferenceApi(api.client).districts('bagmati');
    final stored = await ReferenceApi(api.client).districts('bagmati');

    expect(stored.single.name, 'Kathmandu');

    await expectLater(
      ReferenceApi(api.client).districts('koshi'),
      throwsA(isA<ApiException>()),
    );
  });

  test('plans fall back to the last catalogue that was seen', () async {
    api.onJson('GET', '/plans', envelope(planRows), times: 1);
    api.onError('GET', '/plans', 500, 'server_error', 'Down.', times: 1);

    await PlansApi(api.client).list();
    final stored = await PlansApi(api.client).list();

    expect(stored.single.tier, 'Featured');
    expect(stored.single.priceNpr, 999);
    expect(stored.single.description, 'A pinned listing.');
  });

  test('a fresh load replaces the stored copy', () async {
    api.onJson('GET', '/plans', envelope(planRows), times: 1);
    api.onJson(
      'GET',
      '/plans',
      envelope(<Object?>[
        <String, Object?>{
          'id': 'plan-2',
          'tier': 'Free',
          'name': 'Free',
          'description': null,
          'periodDays': 0,
          'priceNpr': 0,
        },
      ]),
    );

    await PlansApi(api.client).list();
    final newer = await PlansApi(api.client).list();

    expect(newer.single.isFree, isTrue);
    expect(await cache.read('plans'), isA<List<Object?>>());
    expect((await cache.read('plans') as List<Object?>).single,
        <String, Object?>{'id': 'plan-2', 'tier': 'Free', 'name': 'Free', 'description': null, 'periodDays': 0, 'priceNpr': 0});
  });

  test('the calculator catalogue is stored per query', () async {
    api.onJson('GET', '/calculators', envelope(calculatorPage), times: 1);
    api.onError('GET', '/calculators', 500, 'server_error', 'Down.', times: 1);
    api.onError('GET', '/calculators', 500, 'server_error', 'Down.', times: 1);

    final api1 = CalculatorsApi(api.client);
    await api1.list();
    final stored = await api1.list();

    expect(stored.items.single.slug, 'emiflat');
    expect(stored.items.single.category, 'Finance');

    await expectLater(
      api1.list(category: 'Health'),
      throwsA(isA<ApiException>()),
    );
  });
}
