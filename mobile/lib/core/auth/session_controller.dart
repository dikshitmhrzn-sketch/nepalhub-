import 'package:flutter/foundation.dart';

import '../api/api_client.dart';
import '../api/api_exception.dart';
import 'session_user.dart';

enum SessionStatus { loading, authenticated, anonymous }

class SignInResult {
  const SignInResult._(this.user, this.error);

  final SessionUser? user;
  final ApiException? error;

  factory SignInResult.success(SessionUser user) => SignInResult._(user, null);
  factory SignInResult.failure(ApiException error) => SignInResult._(null, error);
}

class SessionController extends ChangeNotifier {
  SessionController(this._api);

  final ApiClient _api;

  SessionStatus _status = SessionStatus.loading;
  SessionUser? _user;
  bool _disposed = false;

  SessionStatus get status => _status;
  SessionUser? get user => _user;
  bool get isAuthenticated => _status == SessionStatus.authenticated;
  bool get isLoading => _status == SessionStatus.loading;
  bool get isAdmin => _user?.isAdmin ?? false;
  bool get isStaff => _user?.isStaff ?? false;

  void _set(SessionStatus status, SessionUser? user) {
    _status = status;
    _user = user;
    _notify();
  }

  void _notify() {
    if (!_disposed) {
      notifyListeners();
    }
  }

  Future<void> restore() async {
    _set(SessionStatus.loading, _user);
    try {
      final session = await _api.getObject<SessionResponse>(
        '/auth/session',
        fromJson: SessionResponse.fromJson,
      );
      if (session.authenticated && session.user != null) {
        _set(SessionStatus.authenticated, session.user);
      } else {
        _set(SessionStatus.anonymous, null);
      }
    } on ApiException {
      _set(SessionStatus.anonymous, null);
    }
  }

  Future<SignInResult> signIn({
    required String email,
    required String password,
    bool rememberMe = true,
  }) async {
    try {
      final signedIn = await _api.postObject<SessionUser>(
        '/auth/login',
        body: {'email': email, 'password': password, 'rememberMe': rememberMe},
        fromJson: SessionUser.fromJson,
      );
      _api.csrf.clear();
      _set(SessionStatus.authenticated, signedIn);
      return SignInResult.success(signedIn);
    } on ApiException catch (error) {
      return SignInResult.failure(error);
    }
  }

  Future<SignInResult> signUp({
    required String fullName,
    required String email,
    required String password,
    String? phoneNumber,
  }) async {
    try {
      final body = <String, Object?>{
        'fullName': fullName,
        'email': email,
        'password': password,
      };
      if (phoneNumber != null && phoneNumber.trim().isNotEmpty) {
        body['phoneNumber'] = phoneNumber.trim();
      }
      final registered = await _api.postObject<SessionUser>(
        '/auth/register',
        body: body,
        fromJson: SessionUser.fromJson,
      );
      _api.csrf.clear();
      _set(SessionStatus.authenticated, registered);
      return SignInResult.success(registered);
    } on ApiException catch (error) {
      return SignInResult.failure(error);
    }
  }

  Future<void> signOut() async {
    try {
      await _api.post('/auth/logout', decode: (_) {});
    } on ApiException {
      // A sign-out that cannot reach the server still ends the local session.
    }
    _api.csrf.clear();
    _set(SessionStatus.anonymous, null);
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
