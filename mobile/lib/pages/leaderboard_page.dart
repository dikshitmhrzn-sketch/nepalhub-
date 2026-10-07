import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/paged_list.dart';
import '../../features/leaderboard/api.dart';

class LeaderboardPage extends StatefulWidget {
  const LeaderboardPage({super.key});

  @override
  State<LeaderboardPage> createState() => _LeaderboardPageState();
}

class _LeaderboardPageState extends State<LeaderboardPage> {
  String _period = 'All';
  int _reloadKey = 0;

  void _select(String period) {
    setState(() {
      _period = period;
      _reloadKey++;
    });
  }

  @override
  Widget build(BuildContext context) {
    final api = LeaderboardApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Leaderboard')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(
              NhSpace.s4,
              NhSpace.s3,
              NhSpace.s4,
              NhSpace.s2,
            ),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Wrap(
                spacing: NhSpace.s2,
                children: [
                  for (final period in leaderboardPeriods)
                    ChoiceChip(
                      label: Text(period == 'All' ? 'All time' : period),
                      selected: _period == period,
                      onSelected: (_) => _select(period),
                    ),
                ],
              ),
            ),
          ),
          Expanded(
            child: PagedListView<LeaderboardEntry>(
              reloadKey: _reloadKey,
              emptyTitle: 'No rankings yet',
              emptyDescription: 'Play the daily quiz to earn XP and climb the board.',
              load: (page) => api.list(page: page, period: _period),
              itemBuilder: (context, entry, _) => Card(
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: NhSpace.s3,
                    vertical: NhSpace.s2,
                  ),
                  child: Row(
                    children: [
                      CircleAvatar(
                        radius: 16,
                        backgroundColor: _rankColor(entry.rank)
                            .withValues(alpha: 0.14),
                        child: Text(
                          '${entry.rank}',
                          style: theme.textTheme.labelMedium?.copyWith(
                            color: _rankColor(entry.rank),
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      const SizedBox(width: NhSpace.s3),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              entry.displayName,
                              style: theme.textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            Text(
                              'Level ${entry.level}',
                              style: theme.textTheme.bodySmall?.copyWith(
                                color: colors.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Text(
                        '${entry.xp} XP',
                        style: theme.textTheme.titleSmall?.copyWith(
                          color: colors.primary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  static Color _rankColor(int rank) {
    switch (rank) {
      case 1:
        return const Color(0xFFB45309);
      case 2:
        return const Color(0xFF64748B);
      case 3:
        return const Color(0xFF92400E);
      default:
        return const Color(0xFF55606F);
    }
  }
}
