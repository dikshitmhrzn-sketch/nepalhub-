import 'dart:convert';
import 'dart:typed_data';

import 'package:cookie_jar/cookie_jar.dart';
import 'package:dio/dio.dart';

import 'package:nepal_hub/core/api/api_client.dart';

class RecordedRequest {
  RecordedRequest(this.method, this.path, this.headers, this.body, this.query);

  final String method;
  final String path;
  final Map<String, dynamic> headers;
  final Object? body;
  final Map<String, dynamic>? query;

  String? header(String name) {
    for (final entry in headers.entries) {
      if (entry.key.toLowerCase() == name.toLowerCase()) {
        return '${entry.value}';
      }
    }
    return null;
  }

  Map<String, dynamic>? get jsonBody {
    final raw = body;
    if (raw is! String || raw.isEmpty) {
      return null;
    }
    return jsonDecode(raw) as Map<String, dynamic>;
  }
}

typedef FakeHandler = Future<ResponseBody> Function(RecordedRequest request);

class FakeHttpClientAdapter implements HttpClientAdapter {
  FakeHttpClientAdapter(this._handle);

  final FakeHandler _handle;

  final List<RecordedRequest> requests = <RecordedRequest>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    Object? body;
    if (requestStream != null) {
      final bytes = await requestStream.fold<List<int>>(
        <int>[],
        (previous, chunk) => previous..addAll(chunk),
      );
      if (bytes.isNotEmpty) {
        body = utf8.decode(bytes, allowMalformed: true);
      }
    }
    final record = RecordedRequest(
      options.method,
      options.path,
      Map<String, dynamic>.from(options.headers),
      body,
      options.queryParameters,
    );
    requests.add(record);
    return _handle(record);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody jsonBody(Object? payload, {int status = 200}) => ResponseBody.fromString(
      jsonEncode(payload),
      status,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>[Headers.jsonContentType],
      },
    );

ResponseBody errorBody(
  int status,
  String code,
  String message, {
  List<Object>? details,
}) {
  final error = <String, Object?>{'code': code, 'message': message};
  if (details != null) {
    error['details'] = details;
  }
  return jsonBody(
    <String, Object?>{'success': false, 'data': null, 'error': error},
    status: status,
  );
}

class TestApi {
  TestApi({this.baseUrl = 'http://test.local/api'}) {
    adapter = FakeHttpClientAdapter(_handle);
    final dio = Dio(BaseOptions(baseUrl: baseUrl, responseType: ResponseType.json));
    dio.httpClientAdapter = adapter;
    client = ApiClient(baseUrl: baseUrl, cookieJar: CookieJar(), dio: dio);
  }

  final String baseUrl;
  late final FakeHttpClientAdapter adapter;
  late final ApiClient client;

  final Map<String, List<FakeHandler>> _routes = <String, List<FakeHandler>>{};

  void on(String method, String path, FakeHandler handler, {int times = 1}) {
    final key = '${method.toUpperCase()} $path';
    final queue = _routes.putIfAbsent(key, () => <FakeHandler>[]);
    for (var i = 0; i < times; i++) {
      queue.add(handler);
    }
  }

  void onJson(
    String method,
    String path,
    Object? payload, {
    int status = 200,
    int times = 1,
  }) =>
      on(
        method,
        path,
        (_) async => jsonBody(payload, status: status),
        times: times,
      );

  void onError(
    String method,
    String path,
    int status,
    String code,
    String message, {
    List<Object>? details,
    int times = 1,
  }) =>
      on(
        method,
        path,
        (_) async => errorBody(status, code, message, details: details),
        times: times,
      );

  Future<ResponseBody> _handle(RecordedRequest request) async {
    final key = '${request.method.toUpperCase()} ${request.path}';
    final queue = _routes[key];
    if (queue == null || queue.isEmpty) {
      return errorBody(404, 'not_found', 'No handler for $key');
    }
    final handler = queue.length == 1 ? queue.first : queue.removeAt(0);
    return handler(request);
  }

  List<RecordedRequest> requestsWhere(String method, String path) => adapter.requests
      .where((request) => request.method.toUpperCase() == method.toUpperCase() && request.path == path)
      .toList();

  int countOf(String method, String path) => requestsWhere(method, path).length;
}
