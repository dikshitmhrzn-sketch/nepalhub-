import '../../core/api/api_client.dart';
import '../../core/offline/cached_load.dart';
import '../../core/offline/reference_cache.dart';
import '../../core/util/formats.dart';

class Plan {
  const Plan({
    required this.id,
    required this.tier,
    required this.name,
    this.description,
    required this.periodDays,
    required this.priceNpr,
  });

  final String id;
  final String tier;
  final String name;
  final String? description;
  final int periodDays;
  final num priceNpr;

  bool get isFree => priceNpr == 0;

  String get priceLabel => isFree ? 'Free' : npr(priceNpr);

  factory Plan.fromJson(Map<String, dynamic> json) => Plan(
        id: '${json['id'] ?? ''}',
        tier: '${json['tier'] ?? 'Free'}',
        name: '${json['name'] ?? ''}',
        description: json['description'] as String?,
        periodDays: (json['periodDays'] as num?)?.toInt() ?? 0,
        priceNpr: json['priceNpr'] as num? ?? 0,
      );

  Map<String, Object?> toJson() => <String, Object?>{
        'id': id,
        'tier': tier,
        'name': name,
        'description': description,
        'periodDays': periodDays,
        'priceNpr': priceNpr,
      };
}

List<Plan> _plans(Object? raw) => raw is List
    ? raw.whereType<Map<String, dynamic>>().map(Plan.fromJson).toList()
    : <Plan>[];

class PlansApi {
  PlansApi(this._client, {this.cache = const ReferenceCache()});

  final ApiClient _client;
  final ReferenceCache cache;

  Future<List<Plan>> list() => cachedLoad<List<Plan>>(
        cache: cache,
        key: 'plans',
        fetch: () => _client.getList<Plan>('/plans', fromJson: Plan.fromJson),
        encode: (plans) => plans.map((plan) => plan.toJson()).toList(),
        decode: _plans,
      );
}
