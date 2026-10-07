import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/auth/session_controller.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/state_message.dart';

class AccountPage extends StatelessWidget {
  const AccountPage({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final session = context.watch<SessionController>();

    return Scaffold(
      appBar: AppBar(title: const Text('Account')),
      body: ListView(
        padding: const EdgeInsets.all(NhSpace.s4),
        children: [
          if (session.isLoading)
            const StateMessage(
              tone: StateMessageTone.loading,
              title: 'Checking your session',
            )
          else if (session.user == null)
            StateMessage(
              tone: StateMessageTone.empty,
              title: 'You are not signed in',
              description: 'Sign in to see your XP, badges, saved listings and payments.',
            )
          else
            Card(
              child: Padding(
                padding: const EdgeInsets.all(NhSpace.s4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      session.user!.fullName,
                      style: theme.textTheme.titleLarge
                          ?.copyWith(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: NhSpace.s1),
                    Text(
                      session.user!.email,
                      style: theme.textTheme.bodyMedium
                          ?.copyWith(color: colors.onSurfaceVariant),
                    ),
                    const SizedBox(height: NhSpace.s3),
                    Text(
                      'Level ${session.user!.level} · ${session.user!.xp} XP',
                      style: theme.textTheme.bodyMedium
                          ?.copyWith(fontWeight: FontWeight.w600),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}
