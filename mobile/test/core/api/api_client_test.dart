import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_exception.dart';

import '../../helpers/fake_adapter.dart';

Future<ApiException> failing(Future<Object?> call) async {
  try {
    await call;
  } on ApiException catch (error) {
    return error;
  }
  throw StateError('the request was expected to fail');
}

void main() {
  late TestApi api;

  setUp(() {
    api = TestApi();
  });

  test('a successful envelope is unwrapped to its data', () async {
    api.onJson('GET', '/reference/provinces', <String, Object?>{
      'success': true,
      'data': [
        {'id': '1', 'code': 'bagmati', 'name': 'Bagmati'},
        {'id': '2', 'code': 'koshi', 'name': 'Koshi'},
      ],
      'error': null,
    });

    final provinces = await api.client.getList<Map<String, dynamic>>(
      '/reference/provinces',
      fromJson: (json) => json,
    );

    expect(provinces, hasLength(2));
    expect(provinces.first['code'], 'bagmati');
  });

  test('a paged envelope keeps every paging field', () async {
    api.onJson('GET', '/jobs', <String, Object?>{
      'success': true,
      'data': {
        'items': [
          {'slug': 'a'},
          {'slug': 'b'},
        ],
        'page': 2,
        'pageSize': 12,
        'totalCount': 30,
        'totalPages': 3,
        'hasPrevious': true,
        'hasNext': true,
      },
      'error': null,
    });

    final page = await api.client.getPaged<Map<String, dynamic>>(
      '/jobs',
      fromJson: (json) => json,
    );

    expect(page.items, hasLength(2));
    expect(page.page, 2);
    expect(page.totalCount, 30);
    expect(page.hasPrevious, isTrue);
    expect(page.hasNext, isTrue);
  });

  test('an unsuccessful envelope becomes an ApiException with its code', () async {
    api.onJson('GET', '/leaderboard', <String, Object?>{
      'success': false,
      'data': null,
      'error': {'code': 'period_invalid', 'message': 'Choose a valid period.'},
    });

    final error = await failing(api.client.get<Object?>('/leaderboard', decode: (json) => json));

    expect(error.code, 'period_invalid');
    expect(error.message, 'Choose a valid period.');
  });

  test('field details survive the round trip', () async {
    api.onJson(
      'GET',
      '/auth/csrf',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'headerName': 'X-CSRF-TOKEN', 'token': 'token-1'},
        'error': null,
      },
    );
    api.onError(
      'POST',
      '/auth/register',
      400,
      'validation_failed',
      'Some fields need attention.',
      details: <Object>[
        {'field': 'email', 'code': 'email_invalid', 'message': 'Enter a valid email.'},
      ],
    );

    final error = await failing(
      api.client.post<Object?>('/auth/register', body: <String, Object>{}, decode: (json) => json),
    );

    expect(error.status, 400);
    expect(error.details.single.field, 'email');
    expect(error.messageFor('email'), 'Enter a valid email.');
    expect(error.fieldMessages['email'], 'Enter a valid email.');
  });

  test('a safe request never asks for a CSRF token', () async {
    api.onJson('GET', '/auth/session', <String, Object?>{
      'success': true,
      'data': <String, Object?>{'authenticated': false, 'user': null},
      'error': null,
    });

    await api.client.getObject<Map<String, dynamic>>(
      '/auth/session',
      fromJson: (json) => json,
    );

    expect(api.countOf('GET', '/auth/csrf'), 0);
    expect(api.adapter.requests.single.header('X-CSRF-TOKEN'), isNull);
  });

  test('an unsafe request fetches the token once and reuses it', () async {
    api.onJson('GET', '/auth/csrf', <String, Object?>{
      'success': true,
      'data': <String, Object?>{'headerName': 'X-CSRF-TOKEN', 'token': 'token-1'},
      'error': null,
    });
    api.onJson(
      'POST',
      '/quiz/daily/submit',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'score': 8},
        'error': null,
      },
      times: 3,
    );

    for (var i = 0; i < 3; i++) {
      await api.client.postObject<Map<String, dynamic>>(
        '/quiz/daily/submit',
        body: <String, Object>{'answers': <Object>[]},
        fromJson: (json) => json,
      );
    }

    expect(api.countOf('GET', '/auth/csrf'), 1);
    expect(api.countOf('POST', '/quiz/daily/submit'), 3);
    expect(
      api.requestsWhere('POST', '/quiz/daily/submit')
          .every((request) => request.header('X-CSRF-TOKEN') == 'token-1'),
      isTrue,
    );
  });

  test('csrf_failed clears the token and retries exactly once', () async {
    api.onJson(
      'GET',
      '/auth/csrf',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'headerName': 'X-CSRF-TOKEN', 'token': 'stale'},
        'error': null,
      },
    );
    api.onJson(
      'GET',
      '/auth/csrf',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'headerName': 'X-CSRF-TOKEN', 'token': 'fresh'},
        'error': null,
      },
    );
    api.onError('POST', '/businesses/claim', 400, 'csrf_failed', 'Reload the page.');
    api.onJson(
      'POST',
      '/businesses/claim',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'status': 'Pending'},
        'error': null,
      },
    );

    final result = await api.client.postObject<Map<String, dynamic>>(
      '/businesses/claim',
      body: <String, Object>{'evidence': 'I run it'},
      fromJson: (json) => json,
    );

    expect(result['status'], 'Pending');
    expect(api.countOf('GET', '/auth/csrf'), 2);
    final attempts = api.requestsWhere('POST', '/businesses/claim');
    expect(attempts, hasLength(2));
    expect(attempts.first.header('X-CSRF-TOKEN'), 'stale');
    expect(attempts.last.header('X-CSRF-TOKEN'), 'fresh');
  });

  test('csrf_failed is not retried on a safe request', () async {
    api.onError('GET', '/jobs', 400, 'csrf_failed', 'Nope.');

    final error = await failing(api.client.get<Object?>('/jobs', decode: (json) => json));

    expect(error.code, 'csrf_failed');
    expect(api.countOf('GET', '/jobs'), 1);
  });

  test('a dropped connection reports a network error', () async {
    api.on('GET', '/health', (_) async => throw const SocketException('down'));

    final error = await failing(api.client.get<Object?>('/health', decode: (json) => json));

    expect(error.isNetwork, isTrue);
    expect(error.status, 0);
  });

  test('a missing handler still answers with an envelope code', () async {
    final error = await failing(api.client.get<Object?>('/nothing', decode: (json) => json));

    expect(error.code, 'not_found');
    expect(error.status, 404);
  });
}
