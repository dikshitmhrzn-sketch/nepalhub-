import 'dart:io';

import 'package:cookie_jar/cookie_jar.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/core/auth/session_controller.dart';
import 'package:nepal_hub/features/badges/api.dart';
import 'package:nepal_hub/features/businesses/api.dart';
import 'package:nepal_hub/features/calculators/api.dart';
import 'package:nepal_hub/features/deals/api.dart';
import 'package:nepal_hub/features/events/api.dart';
import 'package:nepal_hub/features/health/api.dart';
import 'package:nepal_hub/features/jobs/api.dart';
import 'package:nepal_hub/features/leaderboard/api.dart';
import 'package:nepal_hub/features/payments/api.dart';
import 'package:nepal_hub/features/plans/api.dart';
import 'package:nepal_hub/features/quiz/api.dart';
import 'package:nepal_hub/features/reference/api.dart';
import 'package:nepal_hub/features/search/api.dart';
import 'package:nepal_hub/features/students/api.dart';

const String smokeBaseUrl = String.fromEnvironment(
  'SMOKE_API_BASE_URL',
  defaultValue: 'http://localhost:5080/api',
);

Future<bool> backendIsUp() async {
  try {
    final client = HttpClient()..connectionTimeout = const Duration(seconds: 5);
    final request = await client.getUrl(Uri.parse('$smokeBaseUrl/health'));
    final response =
        await request.close().timeout(const Duration(seconds: 5));
    await response.drain<void>();
    client.close(force: true);
    return response.statusCode == 200;
  } catch (_) {
    return false;
  }
}

void main() {
  test('every endpoint the app calls answers the running API', () async {
    if (!await backendIsUp()) {
      markTestSkipped('No API at $smokeBaseUrl - start the backend to run this.');
      return;
    }

    final api = ApiClient(baseUrl: smokeBaseUrl, cookieJar: CookieJar());

    final health = await HealthApi(api).read();
    expect(health.status, 'healthy');
    expect(health.database, 'connected');
    debugPrint('health: ${health.status}/${health.database}');

    final provinces = await ReferenceApi(api).provinces();
    expect(provinces, hasLength(7));
    expect(provinces.every((place) => place.id.isNotEmpty), isTrue);
    debugPrint('provinces: ${provinces.length}');

    final districts =
        await ReferenceApi(api).districts(provinces.first.code);
    expect(districts, isNotEmpty);
    debugPrint('districts in ${provinces.first.name}: ${districts.length}');

    final plans = await PlansApi(api).list();
    expect(plans, isNotEmpty);
    expect(plans.first.name, isNotEmpty);
    debugPrint('plans: ${plans.map((plan) => plan.name).join(', ')}');

    final calculators = await CalculatorsApi(api).list();
    expect(calculators.items, isNotEmpty);
    final slug = calculators.items.first.slug;
    final detail = await CalculatorsApi(api).detail(slug);
    expect(detail.inputs, isNotEmpty);
    final inputs = <String, num>{};
    for (final input in detail.inputs) {
      inputs[input.key] = input.defaultValue ?? input.minimum ?? 1;
    }
    final result = await CalculatorsApi(api).calculate(slug, inputs);
    expect(result.name, isNotEmpty);
    expect(result.summary, isNotEmpty,
        reason: 'the calculation must return summary lines');
    expect(result.summary.map((line) => line.display), everyElement(isNotEmpty));
    expect(result.summary.map((line) => line.label), everyElement(isNotEmpty));
    debugPrint(
        'calculate $slug($inputs): ${result.summary.length} summary lines, '
        'first ${result.summary.first.label} = ${result.summary.first.display}');

    final quiz = await QuizApi(api).daily();
    expect(quiz.questions, isNotEmpty);
    expect(quiz.questions.first.options.length, greaterThanOrEqualTo(2));
    debugPrint(
        'quiz: ${quiz.questions.length} questions, ${quiz.maxXpAvailable} xp available');

    final jobs = await JobsApi(api).list();
    expect(jobs.items, isNotEmpty);
    final job = await JobsApi(api).detail(jobs.items.first.slug);
    expect(job.title, isNotEmpty);
    debugPrint('jobs: ${jobs.totalCount} rows, first "${job.title}"');

    final businesses = await BusinessesApi(api).list();
    expect(businesses.items, isNotEmpty);
    final business =
        await BusinessesApi(api).detail(businesses.items.first.slug);
    expect(business.name, isNotEmpty);
    debugPrint('businesses: ${businesses.totalCount} rows, first "${business.name}"');

    final deals = await DealsApi(api).list();
    expect(deals.items, isNotEmpty);
    final deal = await DealsApi(api).detail(deals.items.first.slug);
    expect(deal.title, isNotEmpty);
    debugPrint('deals: ${deals.totalCount} rows, first "${deal.title}"');

    final events = await EventsApi(api).list();
    expect(events.items, isNotEmpty);
    final event = await EventsApi(api).detail(events.items.first.slug);
    expect(event.title, isNotEmpty);
    debugPrint('events: ${events.totalCount} rows, first "${event.title}"');

    final resources = await StudentsApi(api).list();
    expect(resources.items, isNotEmpty);
    final resource =
        await StudentsApi(api).detail(resources.items.first.slug);
    expect(resource.title, isNotEmpty);
    debugPrint('student resources: ${resources.totalCount} rows, first "${resource.title}"');

    final leaderboard = await LeaderboardApi(api).list();
    expect(leaderboard.items, isNotEmpty);
    expect(leaderboard.items.first.displayName, isNotEmpty);
    debugPrint('leaderboard: ${leaderboard.totalCount} players, top ${leaderboard.items.first.displayName}');

    final search = await SearchApi(api).search(query: 'nepal');
    expect(search.query, 'nepal');
    expect(search.totalCount, greaterThan(0));
    expect(search.groups, isNotEmpty);
    debugPrint('search "nepal": ${search.totalCount} hits in ${search.groups.length} groups');

    final session = SessionController(api);
    var signed = await session.signUp(
      fullName: 'Smoke Tester',
      email: 'smoke@nepalhub.test',
      password: 'Str0ng!pass1',
    );
    if (signed.user == null) {
      signed = await session.signIn(
        email: 'smoke@nepalhub.test',
        password: 'Str0ng!pass1',
      );
    }
    expect(signed.user, isNotNull,
        reason: signed.error?.message ?? 'registration and sign in both failed');
    expect(session.isAuthenticated, isTrue);
    debugPrint('auth: signed in as ${signed.user!.email} (level ${signed.user!.level})');

    final badges = await BadgesApi(api).mine();
    expect(badges.badges, hasLength(3));
    debugPrint('badges: ${badges.awardedCount} of ${badges.badges.length} earned');

    final payments = await PaymentsApi(api).myPayments();
    expect(payments.page, 1);
    debugPrint('payment history: ${payments.totalCount} rows');

    final config = await PaymentsApi(api).config();
    debugPrint('payments provider: available=${config.available} provider=${config.provider}');
  });
}
