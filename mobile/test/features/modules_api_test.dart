import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/util/formats.dart';
import 'package:nepal_hub/features/calculators/api.dart';
import 'package:nepal_hub/features/events/api.dart';
import 'package:nepal_hub/features/jobs/api.dart';
import 'package:nepal_hub/features/leaderboard/api.dart';
import 'package:nepal_hub/features/quiz/api.dart';
import 'package:nepal_hub/features/students/api.dart';

import '../helpers/fake_adapter.dart';

Map<String, Object?> paged(List<Object?> items, {int page = 1, int totalPages = 1}) =>
    <String, Object?>{
      'success': true,
      'data': <String, Object?>{
        'items': items,
        'page': page,
        'pageSize': 24,
        'totalCount': items.length,
        'totalPages': totalPages,
        'hasPrevious': page > 1,
        'hasNext': page < totalPages,
      },
      'error': null,
    };

void main() {
  late TestApi api;

  setUp(() {
    api = TestApi();
  });

  test('jobs list sends every filter as a query parameter', () async {
    api.onJson('GET', '/jobs', paged(<Object?>[
      <String, Object?>{
        'slug': 'flutter-dev',
        'title': 'Flutter developer',
        'organization': 'Himalaya Labs',
        'category': 'InformationTechnology',
        'employmentType': 'FullTime',
        'workMode': 'Remote',
        'districtName': 'Kathmandu',
        'provinceName': 'Bagmati',
        'applicationDeadline': '2026-11-01',
        'status': 'ClosingSoon',
        'createdAt': '2026-10-01T04:00:00Z',
      },
    ]));

    final jobs = await JobsApi(api.client).list(
      page: 2,
      search: '  flutter ',
      category: 'InformationTechnology',
      employmentType: 'FullTime',
      workMode: 'Remote',
      sort: 'Deadline',
    );

    final request = api.requestsWhere('GET', '/jobs').single;
    expect(request.query?['page'], 2);
    expect(request.query?['search'], 'flutter');
    expect(request.query?['category'], 'InformationTechnology');
    expect(request.query?['employmentType'], 'FullTime');
    expect(request.query?['workMode'], 'Remote');
    expect(request.query?['sort'], 'Deadline');
    expect(jobs.items.single.title, 'Flutter developer');
    expect(jobs.items.single.status, 'ClosingSoon');
  });

  test('job detail is fetched from its slug route', () async {
    api.onJson('GET', '/jobs/flutter-dev', <String, Object?>{
      'success': true,
      'data': <String, Object?>{
        'slug': 'flutter-dev',
        'title': 'Flutter developer',
        'organization': 'Himalaya Labs',
        'category': 'InformationTechnology',
        'employmentType': 'FullTime',
        'workMode': 'Remote',
        'districtName': 'Kathmandu',
        'provinceName': 'Bagmati',
        'applicationDeadline': '2026-11-01',
        'status': 'Open',
        'description': 'Build the app.',
        'applicationUrl': 'https://example.com/jobs/1',
      },
      'error': null,
    });

    final job = await JobsApi(api.client).detail('flutter-dev');

    expect(job.title, 'Flutter developer');
    expect(job.applicationUrl, 'https://example.com/jobs/1');
  });

  test('calculator calculate posts the inputs under a csrf header', () async {
    api.onJson(
      'GET',
      '/auth/csrf',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'headerName': 'X-CSRF-TOKEN', 'token': 'token-1'},
        'error': null,
      },
    );
    api.onJson(
      'POST',
      '/calculators/loan-emi/calculate',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{
          'slug': 'loan-emi',
          'name': 'Loan EMI',
          'summary': [
            {'key': 'emi', 'label': 'Monthly payment', 'format': 'Currency', 'value': 12000.5},
          ],
          'breakdown': <Object?>[],
          'notes': ['Rates are indicative.'],
        },
        'error': null,
      },
    );

    final result = await CalculatorsApi(api.client)
        .calculate('loan-emi', <String, num>{'principal': 1000000, 'rate': 12});

    final request = api.requestsWhere('POST', '/calculators/loan-emi/calculate').single;
    expect(request.header('X-CSRF-TOKEN'), 'token-1');
    expect(request.jsonBody?['inputs'], <String, num>{'principal': 1000000, 'rate': 12});
    expect(result.summary.single.display, 'Rs 12,001');
    expect(result.notes.single, 'Rates are indicative.');
  });

  test('event times are read as wall clock, never shifted to utc', () {
    final event = EventSummary.fromJson(<String, Object?>{
      'slug': 'code-camp',
      'title': 'Code camp',
      'category': 'Education',
      'districtName': 'Kathmandu',
      'provinceName': 'Bagmati',
      'startsAt': '2026-10-07T18:30:00',
      'endsAt': '2026-10-07T21:00:00',
      'isOngoing': false,
      'createdAt': '2026-10-01T04:00:00Z',
    });

    expect(formatDateTime(event.startsAt), '7 Oct 2026, 6:30 PM');
    expect(formatRange(event.startsAt, event.endsAt), '7 Oct 2026, 6:30 PM - 9:00 PM');
    expect(wallClock('2026-10-07T18:30:00+05:45').hour, 18);
  });

  test('leaderboard sends the chosen period', () async {
    api.onJson('GET', '/leaderboard', paged(<Object?>[
      <String, Object?>{'rank': 1, 'displayName': 'Asha', 'level': 3, 'xp': 520},
    ]));

    final entries = await LeaderboardApi(api.client).list(period: 'Week');

    expect(api.requestsWhere('GET', '/leaderboard').single.query?['period'], 'Week');
    expect(entries.items.single.displayName, 'Asha');
  });

  test('quiz submit posts one answer per question', () async {
    api.onJson(
      'GET',
      '/auth/csrf',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'headerName': 'X-CSRF-TOKEN', 'token': 'token-1'},
        'error': null,
      },
    );
    api.onJson(
      'POST',
      '/quiz/daily/submit',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{
          'attemptId': 'a-1',
          'quizDate': '2026-10-07',
          'score': 4,
          'maxScore': 5,
          'correctCount': 4,
          'completedAt': '2026-10-07T05:30:00Z',
          'progression': <String, Object?>{
            'recorded': true,
            'xpAwarded': 15,
            'totalXp': 120,
            'level': 2,
            'streakCount': 3,
          },
          'reviews': <Object?>[
            <String, Object?>{
              'questionId': 'q-1',
              'text': 'Capital of Nepal?',
              'category': 'Nepal',
              'selectedOptionId': 'o-1',
              'selectedOptionText': 'Kathmandu',
              'correctOptionId': 'o-1',
              'correctOptionText': 'Kathmandu',
              'isCorrect': true,
              'explanation': 'Kathmandu is the capital.',
            },
          ],
        },
        'error': null,
      },
    );

    final result = await QuizApi(api.client).submit(const [
      QuizAnswer(questionId: 'q-1', optionId: 'o-1'),
    ]);

    final request = api.requestsWhere('POST', '/quiz/daily/submit').single;
    expect(
      (request.jsonBody?['answers'] as List).single,
      <String, String>{'questionId': 'q-1', 'optionId': 'o-1'},
    );
    expect(result.progression?.xpAwarded, 15);
    expect(result.reviews.single.isCorrect, isTrue);
  });

  test('student resource enums keep their display labels', () {
    expect(studentLevelLabels['Grade9to10'], 'Grade 9-10');
    expect(humanize('QuestionPaper'), 'Question Paper');
    expect(humanize('InformationTechnology'), 'Information Technology');
    expect(humanize('NGO'), 'NGO');
    expect(npr(120000), 'Rs 120,000');
  });

  test('students list filters are passed through', () async {
    api.onJson('GET', '/students/resources', paged(<Object?>[
      <String, Object?>{
        'slug': 'physics-notes',
        'title': 'Physics notes',
        'type': 'Note',
        'category': 'Science',
        'level': 'Grade11to12',
        'summary': 'Waves and optics.',
        'hasExternalUrl': false,
        'updatedAt': '2026-09-20T04:00:00Z',
      },
    ]));

    final resources = await StudentsApi(api.client).list(
      search: 'physics',
      type: 'Note',
      level: 'Grade11to12',
      sort: 'Title',
    );

    final query = api.requestsWhere('GET', '/students/resources').single.query!;
    expect(query['type'], 'Note');
    expect(query['level'], 'Grade11to12');
    expect(query['sort'], 'Title');
    expect(resources.items.single.title, 'Physics notes');
  });
}
