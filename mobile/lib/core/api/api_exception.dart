import 'package:flutter/foundation.dart';

@immutable
class FieldError {
  const FieldError({required this.field, required this.code, required this.message});

  final String field;
  final String code;
  final String message;

  factory FieldError.fromJson(Map<String, dynamic> json) => FieldError(
        field: '${json['field'] ?? ''}',
        code: '${json['code'] ?? ''}',
        message: '${json['message'] ?? ''}',
      );
}

class ApiException implements Exception {
  const ApiException(this.status, this.code, this.message, [this.details = const []]);

  final int status;
  final String code;
  final String message;
  final List<FieldError> details;

  bool get isNetwork => code == networkError;
  bool get isTimeout => code == requestTimeout;
  bool get isCancelled => code == requestCancelled;
  bool get isRateLimited => code == rateLimited;
  bool get isCsrfFailed => code == csrfFailed;
  bool get isSessionInvalid => status == 401 || code == sessionInvalid;

  /// The password was right and the address has not been confirmed yet: the sign-in
  /// page should offer the link rather than invite another password attempt.
  bool get isEmailNotConfirmed => code == emailNotConfirmed;

  /// A one-time link ran out — expired, spent, or minted for another purpose. The
  /// server refuses every reason identically, so the only reply is a fresh link.
  bool get isTokenInvalid => code == tokenInvalid;

  String messageFor(String field) {
    for (final detail in details) {
      if (detail.field == field) {
        return detail.message;
      }
    }
    return '';
  }

  Map<String, String> get fieldMessages {
    final messages = <String, String>{};
    for (final detail in details) {
      messages.putIfAbsent(detail.field, () => detail.message);
    }
    return messages;
  }

  @override
  String toString() => 'ApiException($status, $code, $message)';

  static const String networkError = 'network_error';
  static const String requestTimeout = 'request_timeout';
  static const String requestCancelled = 'request_cancelled';
  static const String csrfUnavailable = 'csrf_unavailable';
  static const String csrfFailed = 'csrf_failed';
  static const String rateLimited = 'rate_limited';
  static const String sessionInvalid = 'session_invalid';
  static const String emailNotConfirmed = 'email_not_confirmed';
  static const String tokenInvalid = 'token_invalid';
  static const String unexpectedError = 'unexpected_error';
  static const String serverError = 'server_error';
}
