import '../../core/api/api_client.dart';
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
}

class PlansApi {
  PlansApi(this._client);

  final ApiClient _client;

  Future<List<Plan>> list() => _client.getList<Plan>('/plans', fromJson: Plan.fromJson);
}
