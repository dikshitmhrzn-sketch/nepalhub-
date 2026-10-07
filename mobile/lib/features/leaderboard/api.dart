import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';

const leaderboardPeriods = <String>['All', 'Today', 'Week'];

class LeaderboardEntry {
  const LeaderboardEntry({
    required this.rank,
    required this.displayName,
    required this.level,
    required this.xp,
  });

  final int rank;
  final String displayName;
  final int level;
  final int xp;

  factory LeaderboardEntry.fromJson(Map<String, dynamic> json) => LeaderboardEntry(
        rank: (json['rank'] as num?)?.toInt() ?? 0,
        displayName: '${json['displayName'] ?? ''}',
        level: (json['level'] as num?)?.toInt() ?? 0,
        xp: (json['xp'] as num?)?.toInt() ?? 0,
      );
}

class LeaderboardApi {
  LeaderboardApi(this._client);

  final ApiClient _client;

  Future<PagedResult<LeaderboardEntry>> list({
    int page = 1,
    int pageSize = 50,
    String period = 'All',
  }) =>
      _client.getPaged<LeaderboardEntry>(
        '/leaderboard',
        query: <String, dynamic>{'page': page, 'pageSize': pageSize, 'period': period},
        fromJson: LeaderboardEntry.fromJson,
      );
}
