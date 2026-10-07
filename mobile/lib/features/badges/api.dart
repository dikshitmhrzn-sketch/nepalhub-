import '../../core/api/api_client.dart';

class AccountBadge {
  const AccountBadge({
    required this.code,
    required this.name,
    required this.description,
    required this.icon,
    required this.awarded,
    this.awardedAt,
    required this.current,
    required this.threshold,
  });

  final String code;
  final String name;
  final String description;
  final String icon;
  final bool awarded;
  final String? awardedAt;
  final int current;
  final int threshold;

  double get progress {
    if (awarded) {
      return 1;
    }
    if (threshold <= 0) {
      return 0;
    }
    final value = current / threshold;
    return value > 1 ? 1 : value;
  }

  int get remaining => threshold - current < 0 ? 0 : threshold - current;

  factory AccountBadge.fromJson(Map<String, dynamic> json) => AccountBadge(
        code: '${json['code'] ?? ''}',
        name: '${json['name'] ?? ''}',
        description: '${json['description'] ?? ''}',
        icon: '${json['icon'] ?? ''}',
        awarded: json['awarded'] as bool? ?? false,
        awardedAt: json['awardedAt'] as String?,
        current: (json['current'] as num?)?.toInt() ?? 0,
        threshold: (json['threshold'] as num?)?.toInt() ?? 0,
      );
}

class AccountBadges {
  const AccountBadges({required this.badges, required this.awardedCount});

  final List<AccountBadge> badges;
  final int awardedCount;

  factory AccountBadges.fromJson(Map<String, dynamic> json) => AccountBadges(
        badges: (json['badges'] as List<Object?>? ?? <Object?>[])
            .whereType<Map<String, dynamic>>()
            .map(AccountBadge.fromJson)
            .toList(growable: false),
        awardedCount: (json['awardedCount'] as num?)?.toInt() ?? 0,
      );
}

class BadgesApi {
  BadgesApi(this._client);

  final ApiClient _client;

  Future<AccountBadges> mine() => _client.getObject<AccountBadges>(
        '/account/badges',
        fromJson: AccountBadges.fromJson,
      );
}
