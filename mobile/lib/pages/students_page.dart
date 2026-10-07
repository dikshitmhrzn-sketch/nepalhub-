import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/filter_sheet.dart';
import '../../core/widgets/module_list.dart';
import '../../features/students/api.dart';

class StudentsPage extends StatelessWidget {
  const StudentsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final api = StudentsApi(context.read<ApiClient>());
    return ModuleListScaffold<ResourceSummary>(
      title: 'Student Hub',
      searchHint: 'Search notes, papers, scholarships',
      emptyTitle: 'No resources found',
      emptyDescription: 'Try a different search or clear the filters.',
      filterFields: [
        FilterField('type', 'Type', options: filterOptions(studentTypes)),
        FilterField('category', 'Subject', options: filterOptions(studentCategories)),
        FilterField(
          'level',
          'Level',
          options: filterOptions(studentLevels, labels: studentLevelLabels),
        ),
        FilterField('sort', 'Sort', options: filterOptions(studentSorts)),
      ],
      loader: (page, search, filters) => api.list(
        page: page,
        search: search,
        type: filters['type'],
        category: filters['category'],
        level: filters['level'],
        sort: filters['sort'],
      ),
      itemBuilder: (context, resource, _) => _ResourceCard(
        resource: resource,
        onTap: () => context.push('/students/resources/${resource.slug}'),
      ),
    );
  }
}

class _ResourceCard extends StatelessWidget {
  const _ResourceCard({required this.resource, required this.onTap});

  final ResourceSummary resource;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(NhSpace.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                resource.title,
                style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700),
              ),
              if (resource.summary.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s1),
                Text(
                  resource.summary,
                  style: theme.textTheme.bodyMedium?.copyWith(color: colors.onSurfaceVariant),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
              const SizedBox(height: NhSpace.s2),
              Wrap(
                spacing: NhSpace.s2,
                runSpacing: NhSpace.s1,
                children: [
                  _Pill(label: humanize(resource.type)),
                  _Pill(label: studentLevelLabels[resource.level] ?? humanize(resource.level)),
                  if (resource.hasExternalUrl)
                    _Pill(label: 'External link', color: colors.primary),
                ],
              ),
              const SizedBox(height: NhSpace.s2),
              Text(
                'Updated ${formatDay(resource.updatedAt)}'
                '${resource.expiresOn == null ? '' : '  ·  Expires ${formatDay(resource.expiresOn!)}'}',
                style: theme.textTheme.bodySmall?.copyWith(color: colors.onSurfaceVariant),
              ),
            ],
          ),
        ),
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
      padding: const EdgeInsets.symmetric(horizontal: NhSpace.s2, vertical: 2),
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
