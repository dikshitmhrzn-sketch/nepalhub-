import 'package:flutter/material.dart';

import '../../core/theme/tokens.dart';
import '../../core/widgets/loadable_view.dart';
import 'api.dart';

class HealthCard extends StatelessWidget {
  const HealthCard({super.key, required this.healthApi});

  final HealthApi healthApi;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return LoadableView<ApiHealth>(
      load: healthApi.read,
      builder: (context, health, _) {
        return Card(
          child: Padding(
            padding: const EdgeInsets.all(NhSpace.s4),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'API status',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: NhSpace.s2),
                _StatusRow(label: 'Service', value: health.status, color: _tone(health.status)),
                const SizedBox(height: NhSpace.s1),
                _StatusRow(label: 'Database', value: health.database, color: _tone(health.database)),
                if (health.timestamp != null) ...[
                  const SizedBox(height: NhSpace.s1),
                  _StatusRow(
                    label: 'Checked at',
                    value: _localTime(health.timestamp!),
                    color: colors.onSurfaceVariant,
                  ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }

  static Color _tone(String value) {
    switch (value) {
      case 'healthy':
      case 'connected':
        return const Color(0xFF128A4C);
      case 'unreachable':
      case 'timeout':
        return const Color(0xFFB45309);
      case 'not-configured':
      case 'not_configured':
        return const Color(0xFF9AA3B2);
      default:
        return const Color(0xFF55606F);
    }
  }

  static String _localTime(String timestamp) {
    final parsed = DateTime.tryParse(timestamp)?.toLocal();
    if (parsed == null) {
      return timestamp;
    }
    String two(int value) => value.toString().padLeft(2, '0');
    return '${two(parsed.hour)}:${two(parsed.minute)}:${two(parsed.second)}';
  }
}

class _StatusRow extends StatelessWidget {
  const _StatusRow({required this.label, required this.value, required this.color});

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      children: [
        Expanded(
          child: Text(
            label,
            style: theme.textTheme.bodyMedium
                ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
        ),
        Text(
          value,
          style: theme.textTheme.bodyMedium?.copyWith(
            color: color,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }
}
