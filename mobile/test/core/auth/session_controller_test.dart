import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/auth/session_controller.dart';
import 'package:nepal_hub/core/auth/session_user.dart';

import '../../helpers/fake_adapter.dart';

void main() {
  late TestApi api;
  late SessionController session;

  Map<String, Object?> sessionPayload({
    required bool authenticated,
    Map<String, Object?>? user,
  }) =>
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{'authenticated': authenticated, 'user': user},
        'error': null,
      };

  Map<String, Object?> userPayload({String name = 'Asha Karki'}) => <String, Object?>{
        'id': 'u-1',
        'email': 'asha@example.com',
        'fullName': name,
        'avatarUrl': null,
        'isEmailConfirmed': true,
        'level': 3,
        'xp': 420,
        'roles': <Object>['user', 'business'],
      };

  void routeCsrf({String token = 'token-1'}) => api.onJson(
        'GET',
        '/auth/csrf',
        <String, Object?>{
          'success': true,
          'data': <String, Object?>{'headerName': 'X-CSRF-TOKEN', 'token': token},
          'error': null,
        },
      );

  setUp(() {
    api = TestApi();
    session = SessionController(api.client);
  });

  test('restore hydrates a signed-in session', () async {
    api.onJson('GET', '/auth/session', sessionPayload(authenticated: true, user: userPayload()));

    await session.restore();

    expect(session.status, SessionStatus.authenticated);
    expect(session.user?.fullName, 'Asha Karki');
    expect(session.user?.xp, 420);
    expect(session.user!.roles, ['user', 'business']);
    expect(session.isStaff, isFalse);
  });

  test('restore with no session lands on anonymous', () async {
    api.onJson('GET', '/auth/session', sessionPayload(authenticated: false));

    await session.restore();

    expect(session.status, SessionStatus.anonymous);
    expect(session.user, isNull);
    expect(session.isAuthenticated, isFalse);
  });

  test('a failed probe never blocks public screens', () async {
    api.on(
      'GET',
      '/auth/session',
      (_) async => throw StateError('offline'),
    );

    await session.restore();

    expect(session.status, SessionStatus.anonymous);
  });

  test('sign-in stores the user and drops the old CSRF token', () async {
    api.onJson('GET', '/auth/session', sessionPayload(authenticated: false));
    await session.restore();
    routeCsrf();
    api.onJson('POST', '/auth/login', <String, Object?>{
      'success': true,
      'data': userPayload(),
      'error': null,
    });

    final result = await session.signIn(email: 'asha@example.com', password: 'hunter2!x');

    expect(result.error, isNull);
    expect(session.status, SessionStatus.authenticated);
    expect(session.user?.email, 'asha@example.com');
    expect(api.client.csrf.token, isNull);

    final login = api.requestsWhere('POST', '/auth/login').single;
    expect(login.jsonBody?['rememberMe'], true);
  });

  test('a rejected sign-in reports the server code and changes nothing', () async {
    api.onJson('GET', '/auth/session', sessionPayload(authenticated: false));
    await session.restore();
    routeCsrf();
    api.onError('POST', '/auth/login', 401, 'invalid_credentials', 'Wrong email or password.');

    final result = await session.signIn(email: 'asha@example.com', password: 'wrong');

    expect(result.error?.code, 'invalid_credentials');
    expect(session.status, SessionStatus.anonymous);
  });

  test('sign-up posts the account and signs the user in', () async {
    api.onJson('GET', '/auth/session', sessionPayload(authenticated: false));
    await session.restore();
    routeCsrf();
    api.onJson('POST', '/auth/register', <String, Object?>{
      'success': true,
      'data': userPayload(name: 'New Member'),
      'error': null,
    });

    final result = await session.signUp(
      fullName: 'New Member',
      email: 'new@example.com',
      password: 'Str0ng!pass',
      phoneNumber: '  9800000000 ',
    );

    expect(result.error, isNull);
    expect(session.status, SessionStatus.authenticated);
    final body = api.requestsWhere('POST', '/auth/register').single.jsonBody;
    expect(body?['phoneNumber'], '9800000000');
    expect(body?['fullName'], 'New Member');
  });

  test('sign-out clears the session even if the server is unreachable', () async {
    api.onJson('GET', '/auth/session', sessionPayload(authenticated: true, user: userPayload()));
    await session.restore();
    routeCsrf();
    api.on('POST', '/auth/logout', (_) async => throw StateError('offline'));

    await session.signOut();

    expect(session.status, SessionStatus.anonymous);
    expect(session.user, isNull);
    expect(api.client.csrf.token, isNull);
  });

  test('staff roles are recognised from the session payload', () {
    final moderator = SessionUser.fromJson(<String, Object?>{
      'id': 'u-2',
      'email': 'mod@example.com',
      'fullName': 'Moderator',
      'isEmailConfirmed': true,
      'level': 1,
      'xp': 0,
      'roles': <Object>['user', 'moderator'],
    });
    final administrator = SessionUser.fromJson(<String, Object?>{
      'id': 'u-3',
      'email': 'admin@example.com',
      'fullName': 'Admin',
      'isEmailConfirmed': true,
      'level': 1,
      'xp': 0,
      'roles': <Object>['user', 'admin'],
    });

    expect(moderator.isStaff, isTrue);
    expect(moderator.isAdmin, isFalse);
    expect(administrator.isStaff, isTrue);
    expect(administrator.isAdmin, isTrue);
  });
}
