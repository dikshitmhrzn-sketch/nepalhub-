import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/auth/session_controller.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/loadable_view.dart';
import '../core/widgets/state_message.dart';
import '../features/plans/api.dart';

class AccountPage extends StatelessWidget {
  const AccountPage({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final session = context.watch<SessionController>();
    final plansApi = PlansApi(context.read<ApiClient>());

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
                      'Level ${session.user!.level}  ·  ${session.user!.xp} XP',
                      style: theme.textTheme.bodyMedium
                          ?.copyWith(fontWeight: FontWeight.w600),
                    ),
                  ],
                ),
              ),
            ),
          const SizedBox(height: NhSpace.s5),
          Text(
            'Plans',
            style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: NhSpace.s2),
          SizedBox(
            height: 190,
            child: LoadableView<List<Plan>>(
              load: plansApi.list,
              builder: (context, plans, _) => ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: plans.length,
                separatorBuilder: (context, index) =>
                    const SizedBox(width: NhSpace.s3),
                itemBuilder: (context, index) {
                  final plan = plans[index];
                  return SizedBox(
                    width: 220,
                    child: Card(
                      margin: EdgeInsets.zero,
                      child: Padding(
                        padding: const EdgeInsets.all(NhSpace.s3),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              plan.name,
                              style: theme.textTheme.titleSmall
                                  ?.copyWith(fontWeight: FontWeight.w700),
                            ),
                            const SizedBox(height: NhSpace.s1),
                            Text(
                              plan.priceLabel,
                              style: theme.textTheme.titleMedium?.copyWith(
                                color: colors.primary,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            const SizedBox(height: NhSpace.s1),
                            Text(
                              '${plan.periodDays} days per period',
                              style: theme.textTheme.bodySmall?.copyWith(
                                color: colors.onSurfaceVariant,
                              ),
                            ),
                            const Spacer(),
                            if ((plan.description ?? '').trim().isNotEmpty)
                              Text(
                                plan.description!,
                                style: theme.textTheme.bodySmall,
                                maxLines: 3,
                                overflow: TextOverflow.ellipsis,
                              ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}
