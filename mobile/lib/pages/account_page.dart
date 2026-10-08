import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/api/api_exception.dart';
import '../core/auth/session_controller.dart';
import '../core/auth/validators.dart';
import '../core/theme/tokens.dart';
import '../core/util/formats.dart';
import '../core/widgets/error_banner.dart';
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
            const _ChangePasswordCard(),
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

/// The account changing its own door: the current password is the credential, and the
/// change ends every other session while this one stays exactly where it was.
class _ChangePasswordCard extends StatefulWidget {
  const _ChangePasswordCard();

  @override
  State<_ChangePasswordCard> createState() => _ChangePasswordCardState();
}

class _ChangePasswordCardState extends State<_ChangePasswordCard> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _current = TextEditingController();
  final TextEditingController _next = TextEditingController();
  Map<String, String> _fieldErrors = const <String, String>{};
  ApiException? _error;
  bool _obscurePassword = true;
  bool _submitting = false;
  bool _changed = false;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    setState(() {
      _submitting = true;
      _fieldErrors = const <String, String>{};
      _error = null;
      _changed = false;
    });
    try {
      await context.read<SessionController>().changePassword(
            currentPassword: _current.text,
            newPassword: _next.text,
          );
      if (!mounted) {
        return;
      }
      setState(() {
        _changed = true;
        _submitting = false;
        _current.clear();
        _next.clear();
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _error = error;
        _fieldErrors = error.fieldMessages;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(NhSpace.s4),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Change password',
                style: theme.textTheme.titleMedium
                    ?.copyWith(fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: NhSpace.s1),
              Text(
                'The current password is required. Everywhere the old one was still '
                'signed in is signed out when the new one takes.',
                style: theme.textTheme.bodySmall
                    ?.copyWith(color: colors.onSurfaceVariant),
              ),
              if (_error != null) ...[
                const SizedBox(height: NhSpace.s3),
                ErrorBanner.fromApi(_error!),
              ],
              if (_changed) ...[
                const SizedBox(height: NhSpace.s3),
                Text(
                  'Password changed. Use the new one the next time you sign in.',
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(color: colors.primary, fontWeight: FontWeight.w600),
                ),
              ],
              const SizedBox(height: NhSpace.s3),
              TextFormField(
                controller: _current,
                obscureText: _obscurePassword,
                textInputAction: TextInputAction.next,
                autofillHints: const <String>[AutofillHints.password],
                decoration: const InputDecoration(labelText: 'Current password'),
                validator: (value) => _fieldErrors['currentPassword'] ??
                    signInPasswordValidator(value),
                onChanged: (_) {
                  if (_fieldErrors.containsKey('currentPassword')) {
                    setState(() {
                      _fieldErrors = Map<String, String>.from(_fieldErrors)
                        ..remove('currentPassword');
                    });
                  }
                },
              ),
              const SizedBox(height: NhSpace.s3),
              TextFormField(
                controller: _next,
                obscureText: _obscurePassword,
                textInputAction: TextInputAction.done,
                autofillHints: const <String>[AutofillHints.newPassword],
                decoration: InputDecoration(
                  labelText: 'New password',
                  helperText:
                      'At least 8 characters with upper case, lower case, a digit and a symbol.',
                  suffixIcon: IconButton(
                    tooltip: _obscurePassword ? 'Show password' : 'Hide password',
                    onPressed: () =>
                        setState(() => _obscurePassword = !_obscurePassword),
                    icon: Icon(
                      _obscurePassword
                          ? Icons.visibility_rounded
                          : Icons.visibility_off_rounded,
                    ),
                  ),
                ),
                validator: (value) =>
                    _fieldErrors['password'] ?? passwordValidator(value),
                onFieldSubmitted: (_) => _submit(),
              ),
              const SizedBox(height: NhSpace.s4),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _submitting ? null : _submit,
                  child: _submitting
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Change password'),
                ),
              ),
            ],
          ),
        ),
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
