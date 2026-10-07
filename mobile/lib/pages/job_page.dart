import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/external.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/loadable_view.dart';
import '../../features/jobs/api.dart';

class JobPage extends StatefulWidget {
  const JobPage({super.key, required this.slug});

  final String slug;

  @override
  State<JobPage> createState() => _JobPageState();
}

class _JobPageState extends State<JobPage> {
  @override
  Widget build(BuildContext context) {
    final api = JobsApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Job')),
      body: LoadableView<JobDetail>(
        load: () => api.detail(widget.slug),
        builder: (context, job, _) {
          final canApply = job.applicationUrl.trim().isNotEmpty;
          return ListView(
            padding: const EdgeInsets.all(NhSpace.s4),
            children: [
              Text(
                job.title,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: NhSpace.s1),
              Text(
                job.organization,
                style: theme.textTheme.titleMedium?.copyWith(
                  color: colors.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: NhSpace.s3),
              Wrap(
                spacing: NhSpace.s2,
                runSpacing: NhSpace.s1,
                children: [
                  _Pill(label: humanize(job.employmentType)),
                  _Pill(label: humanize(job.workMode)),
                  _Pill(label: humanize(job.category)),
                  _Pill(
                    label: job.status == 'ClosingSoon' ? 'Closing soon' : 'Open',
                    color: job.status == 'ClosingSoon'
                        ? const Color(0xFFB45309)
                        : const Color(0xFF128A4C),
                  ),
                ],
              ),
              const SizedBox(height: NhSpace.s4),
              _Fact(label: 'Location', value: '${job.districtName}, ${job.provinceName}'),
              _Fact(label: 'Apply by', value: formatDay(job.applicationDeadline)),
              if (job.description.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s4),
                Text(
                  'About this role',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: NhSpace.s2),
                Text(job.description, style: theme.textTheme.bodyMedium),
              ],
              const SizedBox(height: NhSpace.s6),
              FilledButton.icon(
                onPressed: canApply
                    ? () => openExternal(context, job.applicationUrl)
                    : null,
                icon: const Icon(Icons.open_in_new_rounded, size: 18),
                label: const Text('Apply on the employer site'),
              ),
              if (!canApply)
                Padding(
                  padding: const EdgeInsets.only(top: NhSpace.s2),
                  child: Text(
                    'No application link was provided for this job.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                ),
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
