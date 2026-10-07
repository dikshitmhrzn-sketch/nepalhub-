import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/auth/session_controller.dart';
import '../core/theme/tokens.dart';
import '../core/util/formats.dart';
import '../core/widgets/loadable_view.dart';
import '../core/widgets/state_message.dart';
import '../features/badges/api.dart';
import '../features/plans/api.dart';

class AccountPage extends StatelessWidget {
  const AccountPage({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final session = context.watch<SessionController>();
    final plansApi = PlansApi(context.read<ApiClient>());
    final badgesApi = BadgesApi(context.read<ApiClient>());

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
            Column(
              children: [
                StateMessage(
                  tone: StateMessageTone.empty,
                  title: 'You are not signed in',
                  description:
                      'Sign in to see your XP, badges, saved listings and payments.',
                ),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: () => context.push('/login'),
                    child: const Text('Sign in'),
                  ),
                ),
                const SizedBox(height: NhSpace.s2),
                SizedBox(
                  width: double.infinity,
                  child: OutlinedButton(
                    onPressed: () => context.push('/register'),
                    child: const Text('Create an account'),
                  ),
                ),
              ],
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
                    const SizedBox(height: NhSpace.s3),
                    SizedBox(
                      width: double.infinity,
                      child: OutlinedButton.icon(
                        icon: const Icon(Icons.logout_rounded, size: 18),
                        label: const Text('Sign out'),
                        onPressed: () => context.read<SessionController>().signOut(),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          if (session.user != null) ...[
            const SizedBox(height: NhSpace.s5),
            Text(
              'Badges',
              style: theme.textTheme.titleMedium
                  ?.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: NhSpace.s2),
            LoadableView<AccountBadges>(
              load: badgesApi.mine,
              builder: (context, badges, reload) => Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${badges.awardedCount} of ${badges.badges.length} earned',
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: colors.onSurfaceVariant),
                  ),
                  const SizedBox(height: NhSpace.s2),
                  for (final badge in badges.badges) _BadgeTile(badge: badge),
                ],
              ),
            ),
            const SizedBox(height: NhSpace.s4),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                icon: const Icon(Icons.receipt_long_rounded, size: 18),
                label: const Text('Your payments'),
                onPressed: () => context.push('/account/payments'),
              ),
            ),
          ],
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

IconData _badgeIcon(String name) {
  switch (name) {
    case 'book':
      return Icons.menu_book_rounded;
    case 'flame':
      return Icons.local_fire_department_rounded;
    case 'trophy':
      return Icons.emoji_events_rounded;
    default:
      return Icons.military_tech_rounded;
  }
}

class _BadgeTile extends StatelessWidget {
  const _BadgeTile({required this.badge});

  final AccountBadge badge;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;

    return Card(
      margin: const EdgeInsets.only(bottom: NhSpace.s2),
      child: Padding(
        padding: const EdgeInsets.all(NhSpace.s3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            CircleAvatar(
              backgroundColor: badge.awarded
                  ? colors.primaryContainer
                  : colors.surfaceContainerHighest,
              child: Icon(
                _badgeIcon(badge.icon),
                size: 20,
                color: badge.awarded ? colors.onPrimaryContainer : colors.outline,
              ),
            ),
            const SizedBox(width: NhSpace.s3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    badge.name,
                    style: theme.textTheme.titleSmall
                        ?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    badge.description,
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: colors.onSurfaceVariant),
                  ),
                  if (badge.awarded)
                    Padding(
                      padding: const EdgeInsets.only(top: NhSpace.s2),
                      child: Text(
                        badge.awardedAt == null
                            ? 'Earned'
                            : 'Earned ${formatDay(badge.awardedAt!)}',
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: colors.primary, fontWeight: FontWeight.w600),
                      ),
                    )
                  else ...[
                    Padding(
                      padding: const EdgeInsets.only(top: NhSpace.s2),
                      child: Text(
                        '${badge.current} of ${badge.threshold}',
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: colors.onSurfaceVariant),
                      ),
                    ),
                    const SizedBox(height: NhSpace.s1),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(NhSpace.s1),
                      child: LinearProgressIndicator(
                        value: badge.progress,
                        minHeight: 6,
                        backgroundColor: colors.surfaceContainerHighest,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            if (badge.awarded)
              Padding(
                padding: const EdgeInsets.only(left: NhSpace.s2),
                child: Icon(Icons.check_circle_rounded,
                    size: 20, color: colors.primary),
              ),
          ],
        ),
      ),
    );
  }
}
