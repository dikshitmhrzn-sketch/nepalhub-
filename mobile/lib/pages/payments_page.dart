import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/theme/tokens.dart';
import '../core/util/formats.dart';
import '../core/widgets/paged_list.dart';
import '../features/payments/api.dart';

class PaymentsPage extends StatelessWidget {
  const PaymentsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final paymentsApi = PaymentsApi(context.read<ApiClient>());

    return Scaffold(
      appBar: AppBar(title: const Text('Your payments')),
      body: PagedListView<PaymentSummary>(
        load: (page) => paymentsApi.myPayments(page: page),
        emptyTitle: 'No payments yet',
        emptyDescription: 'Event tickets and plans you pay for will be listed here.',
        itemBuilder: (context, payment, index) => Card(
          margin: EdgeInsets.zero,
          child: Padding(
            padding: const EdgeInsets.all(NhSpace.s3),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        payment.description.trim().isEmpty
                            ? payment.reference
                            : payment.description,
                        style: theme.textTheme.titleSmall
                            ?.copyWith(fontWeight: FontWeight.w700),
                      ),
                      const SizedBox(height: NhSpace.s1),
                      Text(
                        '${paymentKindLabels[payment.kind] ?? payment.kind}  ·  '
                        '${formatDay(payment.createdAt)}  ·  '
                        '${paymentStatusLabel(payment.status)}',
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: colors.onSurfaceVariant),
                      ),
                      const SizedBox(height: NhSpace.s1),
                      Text(
                        payment.reference,
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: colors.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: NhSpace.s3),
                Text(
                  npr(payment.amountNpr),
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w800,
                    color: payment.status == 'Succeeded' ? colors.primary : colors.onSurfaceVariant,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
