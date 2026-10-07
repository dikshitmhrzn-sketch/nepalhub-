import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/config/api_environment.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/state_message.dart';

class NotFoundPage extends StatelessWidget {
  const NotFoundPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Not found')),
      body: StateMessage(
        tone: StateMessageTone.empty,
        title: 'This page does not exist',
        description: 'The address you followed is not part of Nepal Hub.',
        actionLabel: 'Go home',
        onAction: () => context.go('/'),
      ),
    );
  }
}

class SettingsPage extends StatelessWidget {
  const SettingsPage({super.key, this.apiBaseUrl = ApiEnvironment.apiBaseUrl});

  final String apiBaseUrl;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(
        padding: const EdgeInsets.all(NhSpace.s4),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(NhSpace.s4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Server',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(height: NhSpace.s2),
                  Text(
                    apiBaseUrl,
                    style: Theme.of(context)
                        .textTheme
                        .bodyMedium
                        ?.copyWith(color: colors.onSurfaceVariant),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: NhSpace.s4),
          Card(
            child: Column(
              children: const [
                ListTile(
                  leading: Icon(Icons.notifications_none_rounded),
                  title: Text('Notifications'),
                  subtitle: Text('Not available yet'),
                ),
                Divider(height: 1),
                ListTile(
                  leading: Icon(Icons.info_outline_rounded),
                  title: Text('About Nepal Hub'),
                  subtitle: Text('Version 1.0.0'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
