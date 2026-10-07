import '../../core/api/api_client.dart';
import '../../core/offline/cached_load.dart';
import '../../core/offline/reference_cache.dart';

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

  Map<String, Object?> toJson() => <String, Object?>{
        'id': id,
        'code': code,
        'name': name,
      };
}

List<PlaceRef> _places(Object? raw) => raw is List
    ? raw.whereType<Map<String, dynamic>>().map(PlaceRef.fromJson).toList()
    : <PlaceRef>[];

class ReferenceApi {
  ReferenceApi(this._client, {this.cache = const ReferenceCache()});

  final ApiClient _client;
  final ReferenceCache cache;

  Future<List<PlaceRef>> provinces() => cachedLoad<List<PlaceRef>>(
        cache: cache,
        key: 'provinces',
        fetch: () => _client.getList<PlaceRef>(
          '/reference/provinces',
          fromJson: PlaceRef.fromJson,
        ),
        encode: (items) => items.map((place) => place.toJson()).toList(),
        decode: _places,
      );

  Future<List<PlaceRef>> districts(String provinceCode) =>
      cachedLoad<List<PlaceRef>>(
        cache: cache,
        key: 'districts.$provinceCode',
        fetch: () => _client.getList<PlaceRef>(
          '/reference/provinces/$provinceCode/districts',
          fromJson: PlaceRef.fromJson,
        ),
        encode: (items) => items.map((place) => place.toJson()).toList(),
        decode: _places,
      );
}
