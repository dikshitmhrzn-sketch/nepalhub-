import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/filter_sheet.dart';
import '../../core/widgets/module_list.dart';
import '../../features/deals/api.dart';

class DealsPage extends StatelessWidget {
  const DealsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final api = DealsApi(context.read<ApiClient>());
    return ModuleListScaffold<DealSummary>(
      title: 'Offers',
      searchHint: 'Search offers',
      emptyTitle: 'No offers found',
      emptyDescription: 'Try a different search or clear the filters.',
      location: true,
      filterFields: [
        FilterField('sort', 'Sort', options: filterOptions(dealSorts)),
      ],
      loader: (page, search, filters) => api.list(
        page: page,
        search: search,
        province: filters['province'],
        district: filters['district'],
        sort: filters['sort'],
      ),
      itemBuilder: (context, deal, _) => _DealCard(
        deal: deal,
        onTap: () => context.push('/deals/${deal.slug}'),
      ),
    );
  }
}

class _DealCard extends StatelessWidget {
  const _DealCard({required this.deal, required this.onTap});

  final DealSummary deal;
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
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      deal.title,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (deal.discountText.trim().isNotEmpty)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: NhSpace.s2,
                        vertical: 2,
                      ),
                      decoration: BoxDecoration(
                        color: colors.primary.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        deal.discountText,
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: colors.primary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                ],
              ),
              if (deal.businessName != null) ...[
                const SizedBox(height: NhSpace.s1),
                Text(
                  deal.businessName!,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: colors.onSurfaceVariant,
                  ),
                ),
              ],
              const SizedBox(height: NhSpace.s2),
              Text(
                '${formatDayShort(deal.startsOn)} - ${formatDay(deal.endsOn)}'
                '${deal.districtName.isEmpty ? '' : '  ·  ${deal.districtName}'}'
                '${deal.daysRemaining > 0 ? '  ·  ${deal.daysRemaining} days left' : ''}',
                style: theme.textTheme.bodySmall?.copyWith(color: colors.onSurfaceVariant),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
