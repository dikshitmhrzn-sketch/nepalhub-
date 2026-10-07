import 'api_exception.dart';

class ApiEnvelope {
  const ApiEnvelope({this.success, this.data, this.error});

  final bool? success;
  final Object? data;
  final ApiEnvelopeError? error;

  factory ApiEnvelope.fromJson(Map<String, dynamic> json) => ApiEnvelope(
        success: json['success'] as bool?,
        data: json['data'],
        error: json['error'] is Map<String, dynamic>
            ? ApiEnvelopeError.fromJson(json['error'] as Map<String, dynamic>)
            : null,
      );
}

class ApiEnvelopeError {
  const ApiEnvelopeError({this.code, this.message, this.details = const []});

  final String? code;
  final String? message;
  final List<FieldError> details;

  factory ApiEnvelopeError.fromJson(Map<String, dynamic> json) => ApiEnvelopeError(
        code: json['code'] as String?,
        message: json['message'] as String?,
        details: json['details'] is List
            ? (json['details'] as List)
                .whereType<Map<String, dynamic>>()
                .map(FieldError.fromJson)
                .toList()
            : const [],
      );
}
