import '../../core/api/api_client.dart';

class PlaceRef {
  const PlaceRef({required this.id, required this.code, required this.name});

  final String id;
  final String code;
  final String name;

  factory PlaceRef.fromJson(Map<String, dynamic> json) => PlaceRef(
        id: '${json['id'] ?? ''}',
        code: '${json['code'] ?? ''}',
        name: '${json['name'] ?? ''}',
      );
}

class ReferenceApi {
  ReferenceApi(this._client);

  final ApiClient _client;

  Future<List<PlaceRef>> provinces() => _client.getList<PlaceRef>(
        '/reference/provinces',
        fromJson: PlaceRef.fromJson,
      );

  Future<List<PlaceRef>> districts(String provinceCode) => _client.getList<PlaceRef>(
        '/reference/provinces/$provinceCode/districts',
        fromJson: PlaceRef.fromJson,
      );
}
