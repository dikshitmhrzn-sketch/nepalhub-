import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/filter_sheet.dart';
import '../../core/widgets/module_list.dart';
import '../../features/calculators/api.dart';

class ToolsPage extends StatelessWidget {
  const ToolsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final api = CalculatorsApi(context.read<ApiClient>());
    return ModuleListScaffold<CalculatorSummary>(
      title: 'Tools & calculators',
      searchHint: 'Search calculators',
      emptyTitle: 'No calculators found',
      emptyDescription: 'Try a different search or clear the filters.',
      filterFields: [
        FilterField('category', 'Category', options: filterOptions(calculatorCategories)),
      ],
      loader: (page, search, filters) =>
          api.list(page: page, search: search, category: filters['category']),
      itemBuilder: (context, calculator, _) => _CalculatorCard(
        calculator: calculator,
        onTap: () => context.push('/calculators/${calculator.slug}'),
      ),
    );
  }
}

class _CalculatorCard extends StatelessWidget {
  const _CalculatorCard({required this.calculator, required this.onTap});

  final CalculatorSummary calculator;
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
          child: Row(
            children: [
              CircleAvatar(
                backgroundColor: colors.primary.withValues(alpha: 0.12),
                child: Icon(Icons.calculate_outlined, color: colors.primary),
              ),
              const SizedBox(width: NhSpace.s3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      calculator.name,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: NhSpace.s1),
                    Text(
                      calculator.summary,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: NhSpace.s1),
                    Text(
                      humanize(calculator.category),
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: colors.primary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, color: colors.onSurfaceVariant),
            ],
          ),
        ),
      ),
    );
  }
}
