import 'dart:async';
import 'dart:io';

import 'package:cookie_jar/cookie_jar.dart';
import 'package:dio/dio.dart';
import 'package:dio_cookie_manager/dio_cookie_manager.dart';

import '../config/api_environment.dart';
import 'api_envelope.dart';
import 'api_exception.dart';
import 'csrf_manager.dart';
import 'paged_result.dart';

typedef JsonDecoder<T> = T Function(Object? json);
typedef ObjectDecoder<T> = T Function(Map<String, dynamic> json);

class ApiClient {
  ApiClient({
    required String baseUrl,
    required CookieJar cookieJar,
    CsrfManager? csrf,
    Dio? dio,
    Duration timeout = ApiEnvironment.requestTimeout,
  }) : _dio = dio ??
            Dio(
              BaseOptions(
                baseUrl: baseUrl,
                connectTimeout: timeout,
                receiveTimeout: timeout,
                sendTimeout: timeout,
                responseType: ResponseType.json,
              ),
            ) {
    _dio.interceptors.add(CookieManager(cookieJar));
    _csrf = csrf ?? CsrfManager(_fetchCsrfToken);
  }

  final Dio _dio;
  late final CsrfManager _csrf;

  static const Set<String> safeMethods = {'GET', 'HEAD', 'OPTIONS', 'TRACE'};

  CsrfManager get csrf => _csrf;

  Future<CsrfToken> _fetchCsrfToken() async {
    final Response<dynamic> response;
    try {
      response = await _dio.get<dynamic>(
        '/auth/csrf',
        options: Options(headers: {'Accept': 'application/json'}),
      );
    } on DioException {
      throw const ApiException(
        0,
        ApiException.csrfUnavailable,
        'Could not start a secure session.',
      );
    }
    final payload = response.data;
    if (payload is Map<String, dynamic> && payload.containsKey('success')) {
      final envelope = ApiEnvelope.fromJson(payload);
      if (envelope.success == true && envelope.data is Map<String, dynamic>) {
        return CsrfToken.fromJson(envelope.data! as Map<String, dynamic>);
      }
    }
    throw const ApiException(
      0,
      ApiException.csrfUnavailable,
      'Could not start a secure session.',
    );
  }

  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? query,
    required JsonDecoder<T> decode,
    CancelToken? cancelToken,
  }) =>
      _send('GET', path, query: query, decode: decode, cancelToken: cancelToken);

  Future<T> post<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required JsonDecoder<T> decode,
    CancelToken? cancelToken,
  }) =>
      _send('POST', path, body: body, query: query, decode: decode, cancelToken: cancelToken);

  Future<T> put<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required JsonDecoder<T> decode,
    CancelToken? cancelToken,
  }) =>
      _send('PUT', path, body: body, query: query, decode: decode, cancelToken: cancelToken);

  Future<T> delete<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required JsonDecoder<T> decode,
    CancelToken? cancelToken,
  }) =>
      _send('DELETE', path, body: body, query: query, decode: decode, cancelToken: cancelToken);

  Future<T> getObject<T>(
    String path, {
    Map<String, dynamic>? query,
    required ObjectDecoder<T> fromJson,
    CancelToken? cancelToken,
  }) =>
      get<T>(path, query: query, decode: asObject(fromJson), cancelToken: cancelToken);

  Future<T> postObject<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required ObjectDecoder<T> fromJson,
    CancelToken? cancelToken,
  }) =>
      post<T>(path, body: body, query: query, decode: asObject(fromJson), cancelToken: cancelToken);

  Future<T> putObject<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required ObjectDecoder<T> fromJson,
    CancelToken? cancelToken,
  }) =>
      put<T>(path, body: body, query: query, decode: asObject(fromJson), cancelToken: cancelToken);

  Future<T> deleteObject<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required ObjectDecoder<T> fromJson,
    CancelToken? cancelToken,
  }) =>
      delete<T>(path, body: body, query: query, decode: asObject(fromJson), cancelToken: cancelToken);

  Future<List<T>> getList<T>(
    String path, {
    Map<String, dynamic>? query,
    required ObjectDecoder<T> fromJson,
    CancelToken? cancelToken,
  }) =>
      get<List<T>>(
        path,
        query: query,
        decode: (json) => decodeList(json, fromJson),
        cancelToken: cancelToken,
      );

  Future<PagedResult<T>> getPaged<T>(
    String path, {
    Map<String, dynamic>? query,
    required ObjectDecoder<T> fromJson,
    CancelToken? cancelToken,
  }) =>
      get<PagedResult<T>>(
        path,
        query: query,
        decode: (json) => PagedResult.fromJson(json! as Map<String, dynamic>, fromJson),
        cancelToken: cancelToken,
      );

  Future<void> postVoid(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    CancelToken? cancelToken,
  }) =>
      post<void>(
        path,
        body: body,
        query: query,
        decode: (_) {},
        cancelToken: cancelToken,
      );

  static JsonDecoder<T> asObject<T>(ObjectDecoder<T> fromJson) =>
      (json) => fromJson(json! as Map<String, dynamic>);

  static List<T> decodeList<T>(Object? json, ObjectDecoder<T> fromJson) {
    final raw = json;
    if (raw is! List) {
      return <T>[];
    }
    return raw.map((item) => fromJson(item! as Map<String, dynamic>)).toList();
  }

  Future<T> _send<T>(
    String method,
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required JsonDecoder<T> decode,
    CancelToken? cancelToken,
    bool retried = false,
  }) async {
    final safe = safeMethods.contains(method.toUpperCase());
    try {
      return await _execute(
        method,
        path,
        body: body,
        query: query,
        decode: decode,
        cancelToken: cancelToken,
        requireCsrf: !safe,
      );
    } on ApiException catch (error) {
      if (!safe && error.isCsrfFailed && !retried) {
        _csrf.clear();
        return _send(
          method,
          path,
          body: body,
          query: query,
          decode: decode,
          cancelToken: cancelToken,
          retried: true,
        );
      }
      rethrow;
    }
  }

  Future<T> _execute<T>(
    String method,
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    required JsonDecoder<T> decode,
    required bool requireCsrf,
    CancelToken? cancelToken,
  }) async {
    final headers = <String, dynamic>{'Accept': 'application/json'};
    if (requireCsrf) {
      headers[_csrf.headerName] = await _csrf.ensure();
    }

    final Response<dynamic> response;
    try {
      response = await _dio.request<dynamic>(
        path,
        data: body,
        queryParameters: query,
        options: Options(method: method, headers: headers),
        cancelToken: cancelToken,
      );
    } on DioException catch (error) {
      throw mapDioException(error);
    }

    return _unwrap(response, decode);
  }

  T _unwrap<T>(Response<dynamic> response, JsonDecoder<T> decode) {
    final dynamic payload = response.data;
    if (payload is Map<String, dynamic> && payload.containsKey('success')) {
      final envelope = ApiEnvelope.fromJson(payload);
      if (envelope.success != true || envelope.data == null) {
        throw fromEnvelope(response.statusCode ?? 0, payload);
      }
      return decode(envelope.data);
    }
    return decode(payload);
  }

  static ApiException fromEnvelope(int status, Map<String, dynamic> payload) {
    final error = payload['error'];
    if (error is Map<String, dynamic>) {
      final details = error['details'] is List
          ? (error['details'] as List)
              .whereType<Map<String, dynamic>>()
              .map(FieldError.fromJson)
              .toList()
          : <FieldError>[];
      return ApiException(
        status,
        '${error['code'] ?? ApiException.unexpectedError}',
        '${error['message'] ?? 'The server returned an unsuccessful response.'}',
        details,
      );
    }
    if (payload['code'] is String && payload['message'] is String) {
      return ApiException(status, payload['code'] as String, payload['message'] as String);
    }
    if (payload['title'] is String) {
      return ApiException(status, ApiException.serverError, payload['title'] as String);
    }
    return ApiException(status, ApiException.unexpectedError, fallbackMessage(status));
  }

  static String fallbackMessage(int status) => status == 0
      ? 'Could not reach the server. Check your connection.'
      : 'Request failed with status $status.';

  static ApiException mapDioException(DioException error) {
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.transformTimeout:
        return const ApiException(
          0,
          ApiException.requestTimeout,
          'The request timed out. Please try again.',
        );
      case DioExceptionType.cancel:
        return const ApiException(0, ApiException.requestCancelled, 'The request was cancelled.');
      case DioExceptionType.badResponse:
        return fromResponse(error.response);
      case DioExceptionType.badCertificate:
        return const ApiException(
          0,
          ApiException.networkError,
          'Could not reach the server. Check your connection.',
        );
      case DioExceptionType.connectionError:
        return const ApiException(
          0,
          ApiException.networkError,
          'Could not reach the server. Check your connection.',
        );
      case DioExceptionType.unknown:
        if (error.response != null) {
          return fromResponse(error.response);
        }
        if (error.error is SocketException || error.error is HttpException) {
          return const ApiException(
            0,
            ApiException.networkError,
            'Could not reach the server. Check your connection.',
          );
        }
        return const ApiException(
          0,
          ApiException.unexpectedError,
          'Something went wrong. Please try again.',
        );
    }
  }

  static ApiException fromResponse(Response<dynamic>? response) {
    final status = response?.statusCode ?? 0;
    final dynamic data = response?.data;
    if (data is Map<String, dynamic>) {
      return fromEnvelope(status, data);
    }
    return ApiException(status, ApiException.unexpectedError, fallbackMessage(status));
  }
}
