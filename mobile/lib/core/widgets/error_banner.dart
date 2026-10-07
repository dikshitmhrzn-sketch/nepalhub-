import 'package:flutter/material.dart';

import '../api/api_exception.dart';
import '../theme/tokens.dart';

class ErrorBanner extends StatelessWidget {
  const ErrorBanner({super.key, required this.title, this.description});

  final String title;
  final String? description;

  factory ErrorBanner.fromApi(ApiException error) => ErrorBanner(
        title: error.message.trim().isEmpty ? 'Request failed' : error.message,
        description: error.details.isEmpty
            ? null
            : error.details.map((detail) => detail.message).join('\n'),
      );

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(NhSpace.s3),
      decoration: BoxDecoration(
        color: colors.errorContainer,
        borderRadius: BorderRadius.circular(NhRadius.md),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.error_outline_rounded, size: 20, color: colors.onErrorContainer),
          const SizedBox(width: NhSpace.s2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: colors.onErrorContainer,
                        fontWeight: FontWeight.w600,
                      ),
                ),
                if ((description ?? '').trim().isNotEmpty) ...[
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    description!,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: colors.onErrorContainer,
                        ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
