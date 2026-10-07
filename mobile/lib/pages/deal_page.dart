import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/external.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/loadable_view.dart';
import '../../core/widgets/report_action.dart';
import '../../features/deals/api.dart';

class DealPage extends StatelessWidget {
  const DealPage({super.key, required this.slug});

  final String slug;

  @override
  Widget build(BuildContext context) {
    final api = DealsApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Offer')),
      body: LoadableView<DealDetail>(
        load: () => api.detail(slug),
        builder: (context, deal, _) {
          final redeemable =
              deal.redemptionUrl != null && deal.redemptionUrl!.trim().isNotEmpty;
          return ListView(
            padding: const EdgeInsets.all(NhSpace.s4),
            children: [
              Text(
                deal.title,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
              if (deal.discountText.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s2),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: NhSpace.s3,
                    vertical: NhSpace.s1,
                  ),
                  decoration: BoxDecoration(
                    color: colors.primary.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    deal.discountText,
                    style: theme.textTheme.titleSmall?.copyWith(
                      color: colors.primary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: NhSpace.s3),
              if (deal.businessName != null && deal.businessName!.trim().isNotEmpty)
                _Fact(label: 'Business', value: deal.businessName!),
              _Fact(
                label: 'Valid',
                value: '${formatDay(deal.startsOn)} - ${formatDay(deal.endsOn)}',
              ),
              _Fact(label: 'District', value: deal.districtName ?? ''),
              _Fact(
                label: 'Status',
                value: !deal.isRunning
                    ? 'Not running'
                    : deal.daysRemaining > 0
                        ? '${deal.daysRemaining} days left'
                        : 'Last day',
              ),
              if (deal.description.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s4),
                Text(
                  'Details',
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: NhSpace.s2),
                Text(deal.description, style: theme.textTheme.bodyMedium),
              ],
              const SizedBox(height: NhSpace.s6),
              FilledButton.icon(
                onPressed: redeemable
                    ? () => openExternal(context, deal.redemptionUrl!)
                    : null,
                icon: const Icon(Icons.local_offer_rounded, size: 18),
                label: const Text('Redeem this offer'),
              ),
              if (!redeemable)
                Padding(
                  padding: const EdgeInsets.only(top: NhSpace.s2),
                  child: Text(
                    'This offer does not have an online redemption link yet.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                ),
              const SizedBox(height: NhSpace.s5),
              ReportAction(targetType: 'Deal', targetId: deal.id),
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
