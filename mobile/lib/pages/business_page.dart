import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/external.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/loadable_view.dart';
import '../../core/widgets/report_action.dart';
import '../../features/businesses/api.dart';

class BusinessPage extends StatelessWidget {
  const BusinessPage({super.key, required this.slug});

  final String slug;

  @override
  Widget build(BuildContext context) {
    final api = BusinessesApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Business')),
      body: LoadableView<BusinessDetail>(
        load: () => api.detail(slug),
        builder: (context, business, _) {
          return ListView(
            padding: const EdgeInsets.all(NhSpace.s4),
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      business.name,
                      style: theme.textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  if (business.isVerified)
                    Padding(
                      padding: const EdgeInsets.only(top: NhSpace.s1),
                      child: Icon(Icons.verified_rounded, color: colors.primary),
                    ),
                ],
              ),
              const SizedBox(height: NhSpace.s2),
              Wrap(
                spacing: NhSpace.s2,
                runSpacing: NhSpace.s1,
                children: [
                  _Pill(label: humanize(business.category)),
                  _Pill(
                    label: business.isOpenNow ? 'Open now' : 'Closed now',
                    color: business.isOpenNow
                        ? const Color(0xFF128A4C)
                        : colors.onSurfaceVariant,
                  ),
                  if (business.isOwner) _Pill(label: 'You own this', color: colors.primary),
                ],
              ),
              const SizedBox(height: NhSpace.s3),
              _Fact(
                label: 'Location',
                value: [
                  if ((business.address ?? '').trim().isNotEmpty) business.address!,
                  if (business.districtName.isNotEmpty) business.districtName,
                  if (business.provinceName.isNotEmpty) business.provinceName,
                ].join(', '),
              ),
              if (business.description.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s3),
                Text(
                  'About',
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: NhSpace.s2),
                Text(business.description, style: theme.textTheme.bodyMedium),
              ],
              if (business.phone != null ||
                  business.email != null ||
                  business.website != null) ...[
                const SizedBox(height: NhSpace.s4),
                Text(
                  'Contact',
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: NhSpace.s2),
                if (business.phone != null && business.phone!.trim().isNotEmpty)
                  _ContactTile(
                    icon: Icons.call_rounded,
                    label: business.phone!,
                    onTap: () => openExternal(context, 'tel:${business.phone!.trim()}'),
                  ),
                if (business.email != null && business.email!.trim().isNotEmpty)
                  _ContactTile(
                    icon: Icons.mail_rounded,
                    label: business.email!,
                    onTap: () => openExternal(context, 'mailto:${business.email!.trim()}'),
                  ),
                if (business.website != null && business.website!.trim().isNotEmpty)
                  _ContactTile(
                    icon: Icons.language_rounded,
                    label: business.website!,
                    onTap: () {
                      final site = business.website!.trim();
                      openExternal(
                        context,
                        site.startsWith('http') ? site : 'https://$site',
                      );
                    },
                  ),
              ],
              if (business.openingHours.isNotEmpty) ...[
                const SizedBox(height: NhSpace.s4),
                Text(
                  'Opening hours',
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: NhSpace.s2),
                Card(
                  margin: EdgeInsets.zero,
                  child: Padding(
                    padding: const EdgeInsets.all(NhSpace.s3),
                    child: Column(
                      children: [
                        for (final hour in business.openingHours)
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: NhSpace.s1),
                            child: Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    hour.dayOfWeek,
                                    style: theme.textTheme.bodyMedium,
                                  ),
                                ),
                                Text(
                                  hour.isClosed
                                      ? 'Closed'
                                      : '${formatTime(hour.opensAt ?? '')} - '
                                          '${formatTime(hour.closesAt ?? '')}',
                                  style: theme.textTheme.bodyMedium?.copyWith(
                                    color: hour.isClosed
                                        ? colors.onSurfaceVariant
                                        : colors.onSurface,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              ],
              const SizedBox(height: NhSpace.s5),
              ReportAction(targetType: 'Business', targetId: business.id),
            ],
          );
        },
      ),
    );
  }
}

class _ContactTile extends StatelessWidget {
  const _ContactTile({required this.icon, required this.label, required this.onTap});

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.only(bottom: NhSpace.s2),
      child: ListTile(
        leading: Icon(icon, color: theme.colorScheme.primary),
        title: Text(label),
        trailing: const Icon(Icons.open_in_new_rounded, size: 18),
        onTap: onTap,
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (value.trim().isEmpty || value.trim() == ', ,') {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: NhSpace.s1),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.label, this.color});

  final String label;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final tint = color ?? Theme.of(context).colorScheme.onSurfaceVariant;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: NhSpace.s2, vertical: 3),
      decoration: BoxDecoration(
        color: tint.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: tint,
              fontWeight: FontWeight.w600,
            ),
      ),
    );
  }
}
