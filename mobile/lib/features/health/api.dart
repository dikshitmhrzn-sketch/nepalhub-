import '../../core/api/api_client.dart';

class ApiHealth {
  const ApiHealth({required this.status, required this.database, this.timestamp});

  final String status;
  final String database;
  final String? timestamp;

  factory ApiHealth.fromJson(Map<String, dynamic> json) => ApiHealth(
        status: '${json['status'] ?? 'unknown'}',
        database: '${json['database'] ?? 'unknown'}',
        timestamp: json['timestamp'] as String?,
      );
}

class HealthApi {
  HealthApi(this._client);

  final ApiClient _client;

  Future<ApiHealth> read() => _client.getObject<ApiHealth>(
        '/health',
        fromJson: ApiHealth.fromJson,
      );
}
