import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/external.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/loadable_view.dart';
import '../../core/widgets/report_action.dart';
import '../../features/students/api.dart';

class StudentResourcePage extends StatelessWidget {
  const StudentResourcePage({super.key, required this.slug});

  final String slug;

  @override
  Widget build(BuildContext context) {
    final api = StudentsApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Student resource')),
      body: LoadableView<ResourceDetail>(
        load: () => api.detail(slug),
        builder: (context, resource, _) {
          final externalUrl = resource.externalUrl;
          return ListView(
            padding: const EdgeInsets.all(NhSpace.s4),
            children: [
              Text(
                resource.title,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: NhSpace.s2),
              Wrap(
                spacing: NhSpace.s2,
                runSpacing: NhSpace.s1,
                children: [
                  _Pill(label: humanize(resource.type)),
                  _Pill(label: humanize(resource.category)),
                  _Pill(label: studentLevelLabels[resource.level] ?? humanize(resource.level)),
                  if (resource.isExpired)
                    _Pill(label: 'Expired', color: const Color(0xFFB45309)),
                ],
              ),
              const SizedBox(height: NhSpace.s3),
              if (resource.academicYear != null)
                _Fact(label: 'Academic year', value: '${resource.academicYear}'),
              if (resource.provider != null && resource.provider!.trim().isNotEmpty)
                _Fact(label: 'Provider', value: resource.provider!),
              if (resource.awardAmountNpr != null)
                _Fact(label: 'Award', value: npr(resource.awardAmountNpr!)),
              if ((resource.eligibility ?? '').trim().isNotEmpty)
                _Fact(label: 'Eligibility', value: resource.eligibility!),
              if (resource.expiresOn != null)
                _Fact(label: 'Expires', value: formatDay(resource.expiresOn!)),
              if (resource.summary.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s4),
                Text(resource.summary, style: theme.textTheme.bodyLarge),
              ],
              if (resource.body.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s3),
                Text(resource.body, style: theme.textTheme.bodyMedium),
              ],
              const SizedBox(height: NhSpace.s6),
              FilledButton.icon(
                onPressed: externalUrl != null && externalUrl.trim().isNotEmpty
                    ? () => openExternal(context, externalUrl)
                    : null,
                icon: const Icon(Icons.open_in_new_rounded, size: 18),
                label: const Text('Open the resource'),
              ),
              if (externalUrl == null || externalUrl.trim().isEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: NhSpace.s2),
                  child: Text(
                    'This resource has no external link.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                ),
              const SizedBox(height: NhSpace.s5),
              ReportAction(targetType: 'StudentResource', targetId: resource.id),
            ],
          );
        },
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
    if (value.trim().isEmpty) {
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
