import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/filter_sheet.dart';
import '../../core/widgets/module_list.dart';
import '../../features/jobs/api.dart';

class JobsPage extends StatelessWidget {
  const JobsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final api = JobsApi(context.read<ApiClient>());
    return ModuleListScaffold<JobSummary>(
      title: 'Jobs',
      searchHint: 'Search jobs or organisations',
      emptyTitle: 'No jobs found',
      emptyDescription: 'Try a different search or clear the filters.',
      location: true,
      filterFields: [
        FilterField('category', 'Category', options: filterOptions(jobCategories)),
        FilterField('employmentType', 'Type', options: filterOptions(jobEmploymentTypes)),
        FilterField('workMode', 'Work mode', options: filterOptions(jobWorkModes)),
        FilterField('sort', 'Sort', options: filterOptions(jobSorts)),
      ],
      loader: (page, search, filters) => api.list(
        page: page,
        search: search,
        province: filters['province'],
        district: filters['district'],
        category: filters['category'],
        employmentType: filters['employmentType'],
        workMode: filters['workMode'],
        sort: filters['sort'],
      ),
      itemBuilder: (context, job, _) => _JobCard(
        job: job,
        onTap: () => context.push('/jobs/${job.slug}'),
      ),
    );
  }
}

class _JobCard extends StatelessWidget {
  const _JobCard({required this.job, required this.onTap});

  final JobSummary job;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final closingSoon = job.status == 'ClosingSoon';
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
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      job.title,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  const SizedBox(width: NhSpace.s2),
                  _Tag(
                    label: closingSoon ? 'Closing soon' : 'Open',
                    color: closingSoon ? const Color(0xFFB45309) : const Color(0xFF128A4C),
                  ),
                ],
              ),
              const SizedBox(height: NhSpace.s1),
              Text(
                job.organization,
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.onSurfaceVariant),
              ),
              const SizedBox(height: NhSpace.s2),
              Wrap(
                spacing: NhSpace.s2,
                runSpacing: NhSpace.s1,
                children: [
                  _Tag(label: humanize(job.employmentType)),
                  _Tag(label: humanize(job.workMode)),
                  _Tag(label: humanize(job.category)),
                ],
              ),
              const SizedBox(height: NhSpace.s2),
              Text(
                '${job.districtName}, ${job.provinceName}  ·  Apply by ${formatDay(job.applicationDeadline)}',
                style: theme.textTheme.bodySmall?.copyWith(color: colors.onSurfaceVariant),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag({required this.label, this.color});

  final String label;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final tint = color ?? colors.onSurfaceVariant;
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
