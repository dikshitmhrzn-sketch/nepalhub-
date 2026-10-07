import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/filter_sheet.dart';
import '../../core/widgets/module_list.dart';
import '../../features/businesses/api.dart';

class BusinessesPage extends StatelessWidget {
  const BusinessesPage({super.key});

  @override
  Widget build(BuildContext context) {
    final api = BusinessesApi(context.read<ApiClient>());
    return ModuleListScaffold<BusinessSummary>(
      title: 'Businesses',
      searchHint: 'Search businesses',
      emptyTitle: 'No businesses found',
      emptyDescription: 'Try a different search or clear the filters.',
      location: true,
      filterFields: [
        FilterField('category', 'Category', options: filterOptions(businessCategories)),
        FilterField('verified', 'Verified', options: const [
          FilterOption('true', 'Verified only'),
          FilterOption('false', 'Unverified only'),
        ]),
        FilterField('sort', 'Sort', options: filterOptions(businessSorts)),
      ],
      loader: (page, search, filters) => api.list(
        page: page,
        search: search,
        province: filters['province'],
        district: filters['district'],
        category: filters['category'],
        verified: filters['verified'],
        sort: filters['sort'],
      ),
      itemBuilder: (context, business, _) => _BusinessCard(
        business: business,
        onTap: () => context.push('/businesses/${business.slug}'),
      ),
    );
  }
}

class _BusinessCard extends StatelessWidget {
  const _BusinessCard({required this.business, required this.onTap});

  final BusinessSummary business;
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
                children: [
                  Expanded(
                    child: Text(
                      business.name,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (business.isVerified)
                    Icon(Icons.verified_rounded, size: 18, color: colors.primary),
                ],
              ),
              const SizedBox(height: NhSpace.s1),
              Text(
                '${humanize(business.category)} · ${business.districtName}, ${business.provinceName}',
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.onSurfaceVariant),
              ),
              if (business.address != null && business.address!.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s1),
                Text(
                  business.address!,
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.onSurfaceVariant),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
              const SizedBox(height: NhSpace.s2),
              Row(
                children: [
                  Icon(
                    business.isOpenNow ? Icons.store_mall_directory_rounded : Icons.lock_clock_rounded,
                    size: 14,
                    color: business.isOpenNow ? const Color(0xFF128A4C) : colors.onSurfaceVariant,
                  ),
                  const SizedBox(width: NhSpace.s1),
                  Text(
                    business.isOpenNow ? 'Open now' : 'Closed now',
                    style: theme.textTheme.labelMedium?.copyWith(
                      color: business.isOpenNow ? const Color(0xFF128A4C) : colors.onSurfaceVariant,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
