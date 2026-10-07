import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/features/badges/api.dart';

import '../helpers/fake_adapter.dart';

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

const Map<String, Object?> badgesPayload = <String, Object?>{
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
};

void main() {
  late TestApi api;

  setUp(() {
    api = TestApi();
  });

  test('the badges come with progress against each threshold', () async {
    api.onJson('GET', '/account/badges', envelope(badgesPayload));

    final result = await BadgesApi(api.client).mine();

    expect(result.awardedCount, 1);
    expect(result.badges, hasLength(2));

    final earned = result.badges.first;
    expect(earned.code, 'seven-day-streak');
    expect(earned.awarded, isTrue);
    expect(earned.awardedAt, isNotNull);
    expect(earned.progress, 1);

    final locked = result.badges.last;
    expect(locked.awarded, isFalse);
    expect(locked.progress, closeTo(0.4, 0.001));
    expect(locked.remaining, 6);
    expect(locked.icon, 'book');
  });

  test('an empty response still parses', () async {
    api.onJson(
      'GET',
      '/account/badges',
      envelope(<String, Object?>{'awardedCount': 0, 'badges': <Object?>[]}),
    );

    final result = await BadgesApi(api.client).mine();

    expect(result.badges, isEmpty);
    expect(result.awardedCount, 0);
  });
}
