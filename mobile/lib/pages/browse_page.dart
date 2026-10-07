import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/theme/tokens.dart';

class BrowsePage extends StatelessWidget {
  const BrowsePage({super.key});

  static const List<_ModuleTile> _modules = [
    _ModuleTile('Tools & calculators', Icons.calculate_outlined, '/tools'),
    _ModuleTile('Daily quiz', Icons.quiz_outlined, '/quiz'),
    _ModuleTile('Student Hub', Icons.menu_book_outlined, '/students'),
    _ModuleTile('Jobs', Icons.work_outline_rounded, '/jobs'),
    _ModuleTile('Businesses', Icons.storefront_outlined, '/businesses'),
    _ModuleTile('Offers', Icons.local_offer_outlined, '/deals'),
    _ModuleTile('Events', Icons.event_outlined, '/events'),
    _ModuleTile('Leaderboard', Icons.emoji_events_outlined, '/leaderboard'),
  ];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Browse')),
      body: ListView(
        padding: const EdgeInsets.all(NhSpace.s4),
        children: [
          Text(
            'Every module on Nepal Hub.',
            style: theme.textTheme.bodyMedium
                ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: NhSpace.s3),
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: NhSpace.s3,
            crossAxisSpacing: NhSpace.s3,
            childAspectRatio: 1.5,
            children: [
              for (final module in _modules)
                _ModuleCard(
                  tile: module,
                  onTap: module.isLive ? () => context.push(module.path) : null,
                ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ModuleTile {
  const _ModuleTile(this.label, this.icon, this.path);

  final String label;
  final IconData icon;
  final String path;

  bool get isLive => true;
}

class _ModuleCard extends StatelessWidget {
  const _ModuleCard({required this.tile, required this.onTap});

  final _ModuleTile tile;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Card(
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(NhSpace.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(tile.icon, color: colors.primary),
              const SizedBox(height: NhSpace.s2),
              Text(
                tile.label,
                style: theme.textTheme.titleSmall?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
