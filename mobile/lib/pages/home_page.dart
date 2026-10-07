import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/config/api_environment.dart';
import '../core/theme/tokens.dart';
import '../features/health/api.dart';
import '../features/health/health_card.dart';

class HomePage extends StatelessWidget {
  const HomePage({super.key, required this.healthApi});

  final HealthApi healthApi;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(
        title: const Text(ApiEnvironment.appName),
        actions: [
          IconButton(
            tooltip: 'Settings',
            icon: const Icon(Icons.settings_outlined),
            onPressed: () => context.go('/settings'),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(NhSpace.s4),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(NhSpace.s5),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Accounts, data, tools, quiz, the Student Hub, Jobs, Businesses, Offers and Events',
                    style: theme.textTheme.labelMedium?.copyWith(
                      color: colors.primary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: NhSpace.s2),
                  Text(
                    'One useful website for everyday Nepal.',
                    style: theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                      height: 1.2,
                    ),
                  ),
                  const SizedBox(height: NhSpace.s3),
                  Text(
                    'Nepal Hub brings Nepal-specific tools and calculators, a daily quiz, '
                    'study resources, jobs, local businesses, offers and events into one place.',
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(height: NhSpace.s4),
                  FilledButton(
                    onPressed: () => context.go('/browse'),
                    child: const Text('Browse the modules'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: NhSpace.s4),
          HealthCard(healthApi: healthApi),
        ],
      ),
    );
  }
}
